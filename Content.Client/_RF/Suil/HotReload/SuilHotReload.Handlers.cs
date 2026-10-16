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

    /// <param name="Condition">What <c>set</c> asks: the state to put the classes in.</param>
    private sealed record PlannedStyle(
        SuilStyleOp Op,
        List<(bool IsPseudo, string Name)> Refs,
        Func<EvalContext, bool>? Condition) : PlannedItem;

    /// <summary>
    /// <c>visible: !#menu.visible</c>: the value of a bool property is worked out when the handler runs.
    /// </summary>
    /// <param name="Accessor">The property, if the type of the target is known; otherwise it is found by the control.</param>
    private sealed record PlannedAssignBool(string Name, SuilStyleAccessor? Accessor, Func<EvalContext, bool> Condition)
        : PlannedItem;

    /// <summary>
    /// <c>min-width: calc(#panel.min-width * 2)</c>: the number is worked out when the handler runs and put in the type of
    /// the property.
    /// </summary>
    private sealed record PlannedAssignNumber(string Name, SuilStyleAccessor? Accessor, Func<EvalContext, double> Expression)
        : PlannedItem;

    /// <summary>
    /// <c>min-width +: 10</c>.
    /// </summary>
    private sealed record PlannedOperator(string Name, SuilStyleAccessor? Accessor, char Operator, Func<EvalContext, double> Operand)
        : PlannedItem;

    /// <summary>
    /// What a bool expression needs to be worked out when an event is raised.
    /// </summary>
    private readonly record struct EvalContext(ViewState State, Control View, object? Argument);

    private sealed record PlannedTarget(bool View, string? Id, IReadOnlyList<string> Path);

    // ---- Planning ----

    /// <summary>
    /// Checks the handlers of the whole tree: every event, method, target and value is known before anything is changed.
    /// </summary>
    private void PlanHandlers(ISuilView view, PlannedElement root, List<string> errors)
    {
        var ids = new Dictionary<string, Type>(StringComparer.Ordinal);
        CollectTypes(root, ids);
        ids[ViewKey] = view.GetType();
        PlanHandlersOf(view, root, ids, errors);
    }

    /// <summary>
    /// The key of the type of the view itself among the ids of the elements: not a valid id, so no element has it.
    /// </summary>
    private const string ViewKey = "&";

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
                        case SuilPropertyItem { Property.Value: [SuilNumValue or SuilBoolValue] } expressionItem:
                        {
                            var plannedExpression = PlanExpressionItem(expressionItem.Property, type, element, evt, ids, errors);

                            if (plannedExpression != null)
                                items.Add(plannedExpression);

                            break;
                        }
                        case SuilOperatorItem op:
                        {
                            var plannedOperator = PlanOperator(op, type, element, evt, ids, errors);

                            if (plannedOperator != null)
                                items.Add(plannedOperator);

                            break;
                        }
                        case SuilPropertyItem property when type == null:
                            items.Add(new PlannedAssign(null, new List<SuilProperty> { property.Property }));
                            break;
                        case SuilPropertyItem property:
                            if (!_suil.TryGetPropertyAccessor(type, property.Property.Name, out var accessor))
                            {
                                errors.Add(At(property.Position, $"{type.Name} has no property '{property.Property.Name}'"));
                                break;
                            }

                            if (!SuilValueConverter.TryConvert(_suil, accessor.Value, property.Property, out var value, out var error))
                            {
                                errors.Add(At(property.Position, $"'{property.Property.Name}': {error}"));
                                break;
                            }

                            items.Add(new PlannedAssign(
                                new List<PlannedProperty> { new(property.Property.Name, accessor.Value, value, false) },
                                null));
                            break;
                        case SuilStyleItem style:
                            var planned = PlanStyle(style, type, element, evt, ids, errors);

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

    /// <summary>
    /// A property assigned an expression. The type of the property tells which kind it is: a bool expression for a bool
    /// property, a number expression for a numeric one (and a lone reference, which the parser reads as a bool one, is a
    /// number if the property is).
    /// </summary>
    private PlannedItem? PlanExpressionItem(
        SuilProperty property,
        Type? type,
        PlannedElement owner,
        SuilEventAccessor evt,
        Dictionary<string, Type> ids,
        List<string> errors)
    {
        SuilStyleAccessor? accessor = null;
        var value = property.Value![0];

        if (type != null)
        {
            if (!_suil.TryGetPropertyAccessor(type, property.Name, out var found))
            {
                errors.Add(At(property.Position, $"{type.Name} has no property '{property.Name}'"));
                return null;
            }

            accessor = found.Value;
        }

        var clr = accessor == null ? null : Nullable.GetUnderlyingType(accessor.Value.ClrType) ?? accessor.Value.ClrType;

        // a number expression, or a reference that is assigned to a number
        if (value is SuilNumValue || clr != null && SuilMath.IsNumeric(clr) && value is SuilBoolValue { Root: SuilBoolReference })
        {
            if (clr != null && !SuilMath.IsNumeric(clr))
            {
                errors.Add(At(property.Position,
                    $"'{property.Name}' is {accessor!.Value.ClrType.Name}: a number expression can only be assigned to a numeric property"));
                return null;
            }

            var node = value is SuilNumValue number
                ? number.Root
                : new SuilNumReference(((SuilBoolReference) ((SuilBoolValue) value).Root).Target, ((SuilBoolReference) ((SuilBoolValue) value).Root).Property);

            var expression = CompileNum(node, owner, ids, errors);
            return expression == null ? null : new PlannedAssignNumber(property.Name, accessor, expression);
        }

        var boolValue = (SuilBoolValue) value;
        var condition = CompileBool(boolValue.Root, owner, evt, ids, errors);

        if (clr != null && clr != typeof(bool))
        {
            errors.Add(At(property.Position,
                $"'{property.Name}' is {accessor!.Value.ClrType.Name}: a bool expression can only be assigned to a bool property"));
            return null;
        }

        return condition == null ? null : new PlannedAssignBool(property.Name, accessor, condition);
    }

    private PlannedItem? PlanOperator(
        SuilOperatorItem item,
        Type? type,
        PlannedElement owner,
        SuilEventAccessor evt,
        Dictionary<string, Type> ids,
        List<string> errors)
    {
        SuilStyleAccessor? accessor = null;

        if (type != null)
        {
            if (!_suil.TryGetPropertyAccessor(type, item.Property, out var found))
            {
                errors.Add(At(item.Position, $"{type.Name} has no property '{item.Property}'"));
                return null;
            }

            if (!SuilMath.IsNumeric(found.Value.ClrType) || found.Value.Getter == null)
            {
                errors.Add(At(item.Position,
                    $"'{item.Property}' is {found.Value.ClrType.Name}: '{item.Operator}:' works on a numeric property that can be read and written"));
                return null;
            }

            accessor = found.Value;
        }

        var operand = CompileNum(item.Operand, owner, ids, errors);
        return operand == null ? null : new PlannedOperator(item.Property, accessor, item.Operator, operand);
    }

    private PlannedStyle? PlanStyle(
        SuilStyleItem style,
        Type? type,
        PlannedElement owner,
        SuilEventAccessor evt,
        Dictionary<string, Type> ids,
        List<string> errors)
    {
        var refs = style.Refs.Select(r => (r.IsPseudo, r.Name.ToString())).ToList();

        if (type != null && !typeof(UiElement).IsAssignableFrom(type) && refs.Any(r => r.IsPseudo))
        {
            errors.Add(At(style.Position, $"{type.Name} has no pseudo-classes: only a control of SUIL does"));
            return null;
        }

        Func<EvalContext, bool>? condition = null;

        if (style.State != null)
        {
            condition = CompileBool(style.State.Expression, owner, evt, ids, errors);

            if (condition == null)
                return null;
        }

        return new PlannedStyle(style.Op, refs, condition);
    }

    // ---- Bool expressions ----

    /// <summary>
    /// Turns a bool expression into a function that works it out when the event is raised. Everything it names is checked
    /// here: the elements, their properties, the arguments of the event. <c>null</c> if something is wrong.
    /// </summary>
    private Func<EvalContext, bool>? CompileBool(
        SuilBoolNode node,
        PlannedElement owner,
        SuilEventAccessor evt,
        Dictionary<string, Type> ids,
        List<string> errors)
    {
        switch (node)
        {
            case SuilBoolConst constant:
            {
                var value = constant.Value;
                return _ => value;
            }
            case SuilBoolNot not:
            {
                var operand = CompileBool(not.Operand, owner, evt, ids, errors);
                return operand == null ? null : context => !operand(context);
            }
            case SuilBoolAnd and:
            {
                var left = CompileBool(and.Left, owner, evt, ids, errors);
                var right = CompileBool(and.Right, owner, evt, ids, errors);
                return left == null || right == null ? null : context => left(context) && right(context);
            }
            case SuilBoolOr or:
            {
                var left = CompileBool(or.Left, owner, evt, ids, errors);
                var right = CompileBool(or.Right, owner, evt, ids, errors);
                return left == null || right == null ? null : context => left(context) || right(context);
            }
            case SuilBoolArgs args:
            {
                if (evt.ArgumentType == null)
                {
                    errors.Add(At(args.Position, $"'args.{args.Member}': {evt.Name} gives no arguments"));
                    return null;
                }

                if (!_suil.Events.TryGetFlag(evt.ArgumentType, args.Member, out var reader))
                {
                    errors.Add(At(args.Position,
                        $"'args.{args.Member}': {evt.ArgumentType.Name} has no public bool member '{args.Member}'"));
                    return null;
                }

                return context => context.Argument != null && reader(context.Argument);
            }
            case SuilBoolReference reference:
                return CompileReference(reference, owner, ids, errors);
            default:
                errors.Add("a variable is left in a bool expression: it must hold true or false");
                return null;
        }
    }

    // ---- Number expressions ----

    /// <summary>
    /// Turns a number expression into a function that works it out when the event is raised. <c>null</c> if something it
    /// names is wrong.
    /// </summary>
    private Func<EvalContext, double>? CompileNum(
        SuilNumNode node,
        PlannedElement owner,
        Dictionary<string, Type> ids,
        List<string> errors)
    {
        switch (node)
        {
            case SuilNumConst constant:
            {
                var value = constant.Value;
                return _ => value;
            }
            case SuilNumNegate negate:
            {
                var operand = CompileNum(negate.Operand, owner, ids, errors);
                return operand == null ? null : context => -operand(context);
            }
            case SuilNumBinary binary:
            {
                var left = CompileNum(binary.Left, owner, ids, errors);
                var right = CompileNum(binary.Right, owner, ids, errors);

                if (left == null || right == null)
                    return null;

                return binary.Operator switch
                {
                    '+' => context => left(context) + right(context),
                    '-' => context => left(context) - right(context),
                    '*' => context => left(context) * right(context),
                    _ => context => left(context) / right(context),
                };
            }
            case SuilNumFunction function:
            {
                var arguments = new List<Func<EvalContext, double>>();

                foreach (var argument in function.Arguments)
                {
                    var compiled = CompileNum(argument, owner, ids, errors);

                    if (compiled == null)
                        return null;

                    arguments.Add(compiled);
                }

                return function.Name switch
                {
                    "abs" => context => Math.Abs(arguments[0](context)),
                    "clamp" => context => Math.Max(arguments[0](context), Math.Min(arguments[1](context), arguments[2](context))),
                    "min" => context => arguments.Min(a => a(context)),
                    _ => context => arguments.Max(a => a(context)),
                };
            }
            case SuilNumReference reference:
                return CompileNumReference(reference, owner, ids, errors);
            default:
                errors.Add("a variable is left in a number expression: it must hold a number");
                return null;
        }
    }

    private Func<EvalContext, double>? CompileNumReference(
        SuilNumReference reference,
        PlannedElement owner,
        Dictionary<string, Type> ids,
        List<string> errors)
    {
        if (!PlanTarget(reference.Target, owner, ids, errors, out var target, out var type))
            return null;

        var property = reference.Property;

        if (type == null)
        {
            return context =>
            {
                var control = Resolve(context.State, target, context.View);
                return control == null ? 0 : ReadNumber(control, property);
            };
        }

        if (!_suil.TryGetPropertyAccessor(type, property, out var accessor)
            || accessor.Value.Getter == null
            || !SuilMath.IsNumeric(accessor.Value.ClrType))
        {
            errors.Add(At(reference.Target.Position, $"{type.Name} has no readable numeric property '{property}'"));
            return null;
        }

        var getter = accessor.Value.Getter;

        return context =>
        {
            var control = Resolve(context.State, target, context.View);
            return control == null ? 0 : SuilMath.ToDouble(getter(control));
        };
    }

    private double ReadNumber(Control control, string property)
    {
        if (_suil.TryGetPropertyAccessor(control.GetType(), property, out var accessor)
            && accessor.Value.Getter is { } getter
            && SuilMath.IsNumeric(accessor.Value.ClrType))
        {
            return SuilMath.ToDouble(getter(control));
        }

        _sawmill.Error($"{control.GetType().Name} has no readable numeric property '{property}'");
        return 0;
    }

    private Func<EvalContext, bool>? CompileReference(
        SuilBoolReference reference,
        PlannedElement owner,
        Dictionary<string, Type> ids,
        List<string> errors)
    {
        if (!PlanTarget(reference.Target, owner, ids, errors, out var target, out var type))
            return null;

        var property = reference.Property;

        // The type is known for an element of this file; behind the fields of a view the property is found by the control.
        if (type == null)
        {
            return context =>
            {
                var control = Resolve(context.State, target, context.View);
                return control != null && ReadBool(control, property);
            };
        }

        if (!_suil.TryGetPropertyAccessor(type, property, out var accessor)
            || accessor.Value.Getter == null
            || accessor.Value.ClrType != typeof(bool))
        {
            errors.Add(At(reference.Target.Position, $"{type.Name} has no readable bool property '{property}'"));
            return null;
        }

        var getter = accessor.Value.Getter;

        return context =>
        {
            var control = Resolve(context.State, target, context.View);
            return control != null && getter(control) is true;
        };
    }

    private bool ReadBool(Control control, string property)
    {
        if (_suil.TryGetPropertyAccessor(control.GetType(), property, out var accessor)
            && accessor.Value.Getter is { } getter
            && accessor.Value.ClrType == typeof(bool))
        {
            return getter(control) is true;
        }

        _sawmill.Error($"{control.GetType().Name} has no readable bool property '{property}'");
        return false;
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
        if (target.IsView)
        {
            planned = new PlannedTarget(true, null, Array.Empty<string>());
            type = ids[ViewKey];
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
        var context = new EvalContext(state, (Control) view, argument);

        foreach (var statement in handler.Statements)
        {
            try
            {
                RunStatement(view, context, statement);
            }
            catch (Exception e)
            {
                _sawmill.Error($"{view.SuilPath}: a handler of {handler.Event.Name} failed: {e}");
            }
        }
    }

    private void RunStatement(ISuilView view, EvalContext context, PlannedStatement statement)
    {
        switch (statement)
        {
            case PlannedCall call:
                view.SuilInvoke(call.Method, call.PassArgument ? context.Argument : null);
                break;
            case PlannedTargetStatement onTarget:
            {
                var target = Resolve(context.State, onTarget.Target, context.View);

                if (target == null)
                {
                    _sawmill.Warning($"{view.SuilPath}: the target of a handler is not found");
                    break;
                }

                foreach (var item in onTarget.Items)
                {
                    RunItem(view, item, target, context);
                }

                break;
            }
        }
    }

    private void RunItem(ISuilView view, PlannedItem item, Control target, EvalContext context)
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
                        && SuilValueConverter.TryConvert(_suil, accessor.Value, property, out var value, out _))
                    {
                        accessor.Value.Setter(target, value!);
                    }
                    else
                    {
                        _sawmill.Error($"{view.SuilPath}: {target.GetType().Name} can't take '{property.Name}' from a handler");
                    }
                }

                break;
            case PlannedAssignNumber number:
            {
                if (!TryNumericAccessor(view, target, number.Name, number.Accessor, requireGetter: false, out var numeric))
                    break;

                numeric.Setter(target, SuilMath.FromDouble(numeric.ClrType, number.Expression(context))!);
                break;
            }
            case PlannedOperator op:
            {
                if (!TryNumericAccessor(view, target, op.Name, op.Accessor, requireGetter: true, out var numeric))
                    break;

                var current = SuilMath.ToDouble(numeric.Getter!(target));
                var operand = op.Operand(context);

                var result = op.Operator switch
                {
                    '+' => current + operand,
                    '-' => current - operand,
                    '*' => current * operand,
                    _ => current / operand,
                };

                numeric.Setter(target, SuilMath.FromDouble(numeric.ClrType, result)!);
                break;
            }
            case PlannedAssignBool assign:
            {
                var accessor = assign.Accessor;

                if (accessor == null)
                {
                    if (!_suil.TryGetPropertyAccessor(target.GetType(), assign.Name, out var found)
                        || (Nullable.GetUnderlyingType(found.Value.ClrType) ?? found.Value.ClrType) != typeof(bool))
                    {
                        _sawmill.Error($"{view.SuilPath}: {target.GetType().Name} has no bool property '{assign.Name}' to assign");
                        break;
                    }

                    accessor = found.Value;
                }

                accessor.Value.Setter(target, assign.Condition(context));
                break;
            }
            case PlannedStyle style:
            {
                // `set` asks once for all of its classes
                var state = style.Op == SuilStyleOp.Set && style.Condition!(context);

                foreach (var (isPseudo, name) in style.Refs)
                {
                    var on = style.Op switch
                    {
                        SuilStyleOp.Add => true,
                        SuilStyleOp.Remove => false,
                        _ => state,
                    };

                    ChangeClass(view, target, isPseudo, name, on);
                }

                break;
            }
        }
    }

    private bool TryNumericAccessor(
        ISuilView view,
        Control target,
        string name,
        SuilStyleAccessor? planned,
        bool requireGetter,
        out SuilStyleAccessor accessor)
    {
        accessor = default;

        if (planned is { } known)
        {
            accessor = known;
            return true;
        }

        if (_suil.TryGetPropertyAccessor(target.GetType(), name, out var found)
            && SuilMath.IsNumeric(found.Value.ClrType)
            && (!requireGetter || found.Value.Getter != null))
        {
            accessor = found.Value;
            return true;
        }

        _sawmill.Error($"{view.SuilPath}: {target.GetType().Name} has no numeric property '{name}' to change");
        return false;
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
    /// The control of a target: the view itself, the one with the id, or the one behind the fields of a view.
    /// </summary>
    private static Control? Resolve(ViewState state, PlannedTarget target, Control view)
    {
        if (target.View)
            return view;

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
