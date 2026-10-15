using System.Linq;
using Content.Suil;
using Robust.Client.UserInterface;

namespace Content.Client._RF.Suil.HotReload;

public sealed partial class SuilHotReload
{
    private sealed class PlannedHandler
    {
        public required SuilEventAccessor Event;
        public readonly List<PlannedStatement> Statements = new();
    }

    private abstract record PlannedStatement;

    private sealed record PlannedCall(string Method, bool PassArgument) : PlannedStatement;

    /// <summary>
    /// <c>#target (...)</c>: what to change on one element.
    /// </summary>
    private sealed record PlannedTargetStatement(PlannedTarget Target, List<PlannedItem> Items) : PlannedStatement;

    private abstract record PlannedItem;

    /// <param name="Properties">Converted when the type of the target is known; otherwise <paramref name="Raw"/> is.</param>
    /// <param name="Raw">Converted when the handler runs, for a target that is reached through the fields of a view.</param>
    private sealed record PlannedAssign(List<PlannedProperty>? Properties, List<SuilProperty>? Raw) : PlannedItem;

    /// <param name="State">What <c>set</c> reads from the arguments of the event, if it does.</param>
    private sealed record PlannedStyle(
        SuilStyleOp Op,
        List<(bool IsPseudo, string Name)> Refs,
        bool? Constant,
        Func<object, bool>? State) : PlannedItem;

    private sealed record PlannedTarget(bool Sender, string? Id, IReadOnlyList<string> Path);

    // ---- Planning ----

    /// <summary>
    /// Checks the handlers of the whole tree: every event, method, target and value is known before anything is changed.
    /// </summary>
    private void PlanHandlers(ISuilView view, PlannedElement root, List<string> errors)
    {
        var ids = new Dictionary<string, Type>(StringComparer.Ordinal);
        CollectTypes(root, ids);
        PlanHandlersOf(view, root, ids, errors);
    }

    private static void CollectTypes(PlannedElement element, Dictionary<string, Type> into)
    {
        if (element.Id != null)
            into[element.Id] = element.Type;

        foreach (var child in element.Children)
        {
            CollectTypes(child, into);
        }
    }

    private void PlanHandlersOf(ISuilView view, PlannedElement element, Dictionary<string, Type> ids, List<string> errors)
    {
        foreach (var source in element.Element.Events)
        {
            if (!_suil.Events.TryGetEvent(element.Type, source.Event, out var evt))
            {
                errors.Add(At(source.Position, $"{element.Type.Name} has no event '{source.Event}'"));
                continue;
            }

            var handler = new PlannedHandler { Event = evt };

            foreach (var statement in source.Statements)
            {
                var planned = PlanStatement(view, element, evt, statement, ids, errors);

                if (planned != null)
                    handler.Statements.Add(planned);
            }

            element.Handlers.Add(handler);
        }

        foreach (var child in element.Children)
        {
            PlanHandlersOf(view, child, ids, errors);
        }
    }

    private PlannedStatement? PlanStatement(
        ISuilView view,
        PlannedElement element,
        SuilEventAccessor evt,
        ISuilStatement statement,
        Dictionary<string, Type> ids,
        List<string> errors)
    {
        switch (statement)
        {
            case SuilCallStatement call:
            {
                if (!view.SuilTryGetMethod(call.Method, out var argumentType))
                {
                    errors.Add(At(call.Position,
                        $"'@{call.Method}': {view.GetType().Name} has no such method that a handler can call: " +
                        "a method is in the code, build the project"));
                    return null;
                }

                if (argumentType != null && (evt.ArgumentType == null || !argumentType.IsAssignableFrom(evt.ArgumentType)))
                {
                    errors.Add(At(call.Position,
                        $"'@{call.Method}' takes {argumentType.Name}, and {evt.Name} gives " +
                        (evt.ArgumentType == null ? "nothing" : evt.ArgumentType.Name)));
                    return null;
                }

                return new PlannedCall(call.Method, argumentType != null);
            }
            case SuilTargetStatement targetStatement:
            {
                if (!PlanTarget(targetStatement.Target, element, ids, errors, out var target, out var type))
                    return null;

                // the items keep the order of the file, like the statements of the generated code do
                var items = new List<PlannedItem>();

                foreach (var item in targetStatement.Items)
                {
                    switch (item)
                    {
                        case SuilPropertyItem property when type == null:
                            items.Add(new PlannedAssign(null, new List<SuilProperty> { property.Property }));
                            break;
                        case SuilPropertyItem property:
                            if (!_suil.TryGetPropertyAccessor(type, property.Property.Name, out var accessor))
                            {
                                errors.Add(At(property.Position, $"{type.Name} has no property '{property.Property.Name}'"));
                                break;
                            }

                            if (!TryConvert(accessor.Value, property.Property, out var value, out var error))
                            {
                                errors.Add(At(property.Position, $"'{property.Property.Name}': {error}"));
                                break;
                            }

                            items.Add(new PlannedAssign(
                                new List<PlannedProperty> { new(property.Property.Name, accessor.Value, value, false) },
                                null));
                            break;
                        case SuilStyleItem style:
                            var planned = PlanStyle(style, type, evt, errors);

                            if (planned != null)
                                items.Add(planned);

                            break;
                    }
                }

                return new PlannedTargetStatement(target, items);
            }
            default:
                return null;
        }
    }

    private PlannedStyle? PlanStyle(SuilStyleItem style, Type? type, SuilEventAccessor evt, List<string> errors)
    {
        var refs = style.Refs.Select(r => (r.IsPseudo, r.Name.ToString())).ToList();

        if (type != null && !typeof(UiElement).IsAssignableFrom(type) && refs.Any(r => r.IsPseudo))
        {
            errors.Add(At(style.Position, $"{type.Name} has no pseudo-classes: only a control of SUIL does"));
            return null;
        }

        Func<object, bool>? flag = null;

        if (style.State?.ArgsMember is { } member)
        {
            if (evt.ArgumentType == null || !_suil.Events.TryGetFlag(evt.ArgumentType, member, out flag))
            {
                errors.Add(At(style.Position,
                    $"'args.{member}': {evt.Name} gives " +
                    (evt.ArgumentType == null ? "no arguments" : $"{evt.ArgumentType.Name}, which has no public bool member '{member}'")));
                return null;
            }
        }

        return new PlannedStyle(style.Op, refs, style.State?.Constant, flag);
    }

    /// <summary>
    /// Finds the element a target names in the file. <paramref name="type"/> is its type if the file says it, which it
    /// doesn't for an element behind the fields of a view.
    /// </summary>
    private bool PlanTarget(
        SuilTarget target,
        PlannedElement owner,
        Dictionary<string, Type> ids,
        List<string> errors,
        out PlannedTarget planned,
        out Type? type)
    {
        if (target.IsSender)
        {
            planned = new PlannedTarget(true, null, Array.Empty<string>());
            type = owner.Type;
            return true;
        }

        var id = target.Id!.ToString();
        planned = new PlannedTarget(false, id, target.Path);
        type = null;

        if (!ids.TryGetValue(id, out var found))
        {
            errors.Add(At(target.Position, $"There is no element with the id '{id}' in this view"));
            return false;
        }

        type = target.Path.Count == 0 ? found : null;
        return true;
    }

    // ---- Running ----

    private void Subscribe(ISuilView view, Control control, PlannedElement plan, ViewState state)
    {
        foreach (var handler in plan.Handlers)
        {
            var planned = handler;
            var added = planned.Event.Subscribe(control, argument => Run(view, state, planned, control, argument));
            state.Subscriptions.Add((control, planned.Event, added));
        }
    }

    private void Run(ISuilView view, ViewState state, PlannedHandler handler, Control sender, object? argument)
    {
        foreach (var statement in handler.Statements)
        {
            try
            {
                RunStatement(view, state, statement, sender, argument);
            }
            catch (Exception e)
            {
                _sawmill.Error($"{view.SuilPath}: a handler of {handler.Event.Name} failed: {e}");
            }
        }
    }

    private void RunStatement(ISuilView view, ViewState state, PlannedStatement statement, Control sender, object? argument)
    {
        switch (statement)
        {
            case PlannedCall call:
                view.SuilInvoke(call.Method, call.PassArgument ? argument : null);
                break;
            case PlannedTargetStatement onTarget:
            {
                var target = Resolve(state, onTarget.Target, sender);

                if (target == null)
                {
                    _sawmill.Warning($"{view.SuilPath}: the target of a handler is not found");
                    break;
                }

                foreach (var item in onTarget.Items)
                {
                    RunItem(view, item, target, argument);
                }

                break;
            }
        }
    }

    private void RunItem(ISuilView view, PlannedItem item, Control target, object? argument)
    {
        switch (item)
        {
            case PlannedAssign { Properties: { } properties }:
                foreach (var property in properties)
                {
                    property.Accessor.Setter(target, property.Value!);
                }

                break;
            case PlannedAssign { Raw: { } raw }:
                foreach (var property in raw)
                {
                    if (_suil.TryGetPropertyAccessor(target.GetType(), property.Name, out var accessor)
                        && TryConvert(accessor.Value, property, out var value, out _))
                    {
                        accessor.Value.Setter(target, value!);
                    }
                    else
                    {
                        _sawmill.Error($"{view.SuilPath}: {target.GetType().Name} can't take '{property.Name}' from a handler");
                    }
                }

                break;
            case PlannedStyle style:
                foreach (var (isPseudo, name) in style.Refs)
                {
                    var on = style.Op switch
                    {
                        SuilStyleOp.Add => true,
                        SuilStyleOp.Remove => false,
                        _ => style.Constant ?? style.State!(argument!),
                    };

                    ChangeClass(view, target, isPseudo, name, on);
                }

                break;
        }
    }

    private void ChangeClass(ISuilView view, Control target, bool isPseudo, string name, bool on)
    {
        if (target is UiElement element)
        {
            if (isPseudo)
                element.SetSuilPseudoStyleClass(name, on);
            else if (on)
                element.AddSuilStyleClass(name);
            else
                element.RemoveSuilStyleClass(name);

            return;
        }

        if (isPseudo)
        {
            _sawmill.Error($"{view.SuilPath}: {target.GetType().Name} has no pseudo-classes");
            return;
        }

        if (on)
            target.AddStyleClass(name);
        else
            target.RemoveStyleClass(name);
    }

    /// <summary>
    /// The control of a target: the one the event was raised on, the one with the id, or the one behind the fields of a view.
    /// </summary>
    private static Control? Resolve(ViewState state, PlannedTarget target, Control sender)
    {
        if (target.Sender)
            return sender;

        if (!state.Controls.TryGetValue(target.Id!, out var current))
            return null;

        foreach (var field in target.Path)
        {
            current = (current as ISuilView)?.SuilGetField(field) as Control;

            if (current == null)
                return null;
        }

        return current;
    }
}
