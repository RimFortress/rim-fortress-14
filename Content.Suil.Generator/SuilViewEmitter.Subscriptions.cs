using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Content.Suil.Generator;

/// <summary>
/// The subscriptions of a view, <c>on @Systems.Hunger.Changed for hunger-bar =&gt; ...</c>: the view subscribes when it enters
/// the tree of the controls and unsubscribes when it leaves it.
/// </summary>
internal sealed partial class SuilViewEmitter
{
    private readonly StringBuilder _subscriptionFields = new();
    private readonly StringBuilder _subscribe = new();
    private readonly StringBuilder _unsubscribe = new();
    private int _subscriptionCounter;
    private SuilPosition _firstSubscription;

    private readonly StringBuilder _bindFields = new();
    private int _hookCount;

    private bool HasSubscriptions => _subscriptionCounter > 0 || _hookCount > 0;

    private void EmitSubscriptions(SuilElement root, IReadOnlyList<SuilSubscription> subscriptions)
    {
        var pending = new PendingHandlers("this", viewClass, root);

        if (subscriptions.Count > 0)
            _firstSubscription = subscriptions[0].Position;

        foreach (var subscription in subscriptions)
        {
            EmitSubscription(pending, subscription);
        }
    }

    private void EmitSubscription(PendingHandlers pending, SuilSubscription subscription)
    {
        var (owner, evt, delegateType, argsType) = ResolveEvent(subscription.Scope, subscription.Name, subscription.Event, subscription.Position);
        var lambda = StatementsOf(pending, subscription.Handler, argsType);

        AddSourceSubscription(subscription.Scope, owner, evt, delegateType, argsType, lambda, null);
    }

    /// <summary>
    /// The class of a controller or of a system and its event, for a subscription or a binding.
    /// </summary>
    private (INamedTypeSymbol Owner, IEventSymbol Event, INamedTypeSymbol Delegate, ITypeSymbol? Arguments) ResolveEvent(
        SuilCallScope scope,
        string name,
        string eventName,
        SuilPosition position)
    {
        var scopeName = scope == SuilCallScope.Controller ? "Controllers" : "Systems";
        var owner = SuilCallClasses.Find(compilation, scope, name, out var candidates)
                    ?? throw new SuilGenerationException(SuilDiagnostics.UnknownCallClass, position,
                        scopeName,
                        name,
                        candidates.Count == 0
                            ? "there is no such " + (scope == SuilCallScope.Controller ? "UI controller (a class named NameUiController)" : "system (a class named NameSystem)")
                            : "it can be " + string.Join(", ", candidates.Select(c => c.ToDisplayString()).OrderBy(n => n, StringComparer.Ordinal)));

        var evt = FindClassEvent(owner, eventName)
                  ?? throw new SuilGenerationException(SuilDiagnostics.UnknownClassEvent, position,
                      owner.ToDisplayString(), eventName);

        var delegateType = evt.Type as INamedTypeSymbol;
        var invoke = delegateType?.DelegateInvokeMethod;

        if (delegateType == null || invoke == null || !invoke.ReturnsVoid || invoke.Parameters.Length > 1)
        {
            throw new SuilGenerationException(SuilDiagnostics.UnsupportedClassEvent, position,
                evt.Name, owner.ToDisplayString(), evt.Type.ToDisplayString());
        }

        return (owner, evt, delegateType, invoke.Parameters.Length == 1 ? invoke.Parameters[0].Type : null);
    }

    /// <summary>
    /// Subscribes the view to the event while it is in the tree. <paramref name="initial"/> is a line that is run right
    /// after the subscription, <c>null</c> for none.
    /// </summary>
    private void AddSourceSubscription(
        SuilCallScope scope,
        INamedTypeSymbol owner,
        IEventSymbol evt,
        INamedTypeSymbol delegateType,
        ITypeSymbol? argsType,
        StringBuilder lambda,
        string? initial)
    {
        var index = _subscriptionCounter++;
        var delegateName = delegateType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var ownerName = owner.ToDisplayString(TypeFormat);
        var source = $"__suilSource{index}";
        var handlerField = $"__suilOn{index}";

        var resolve = scope == SuilCallScope.Controller
            ? $"global::Robust.Shared.IoC.IoCManager.Resolve<global::Robust.Client.UserInterface.IUserInterfaceManager>().GetUIController<{ownerName}>()"
            : $"global::Robust.Shared.IoC.IoCManager.Resolve<global::Robust.Shared.GameObjects.IEntityManager>().System<{ownerName}>()";

        // The source is kept: when the view leaves the tree at the shutdown, the controller or the system may be gone.
        _subscriptionFields.AppendLine($"        private {ownerName}? {source};");
        _subscriptionFields.AppendLine($"        private {delegateName}? {handlerField};");

        _subscribe.AppendLine($"            if ({handlerField} == null)");
        _subscribe.AppendLine("            {");
        _subscribe.AppendLine($"                {source} = {resolve};");
        _subscribe.AppendLine($"                {handlerField} = {(argsType == null ? "()" : "__args")} =>");
        _subscribe.AppendLine("                {");
        _subscribe.Append(Indent(lambda));
        _subscribe.AppendLine("                };");
        _subscribe.AppendLine($"                {source}.{evt.Name} += {handlerField};");

        if (initial != null)
            _subscribe.AppendLine($"                {initial}");

        _subscribe.AppendLine("            }");
        _subscribe.AppendLine();

        _unsubscribe.AppendLine($"            if ({handlerField} != null)");
        _unsubscribe.AppendLine("            {");
        _unsubscribe.AppendLine($"                {source}!.{evt.Name} -= {handlerField};");
        _unsubscribe.AppendLine($"                {handlerField} = null;");
        _unsubscribe.AppendLine($"                {source} = null;");
        _unsubscribe.AppendLine("            }");
        _unsubscribe.AppendLine();
    }

    // ---- Bindings ----

    private readonly List<PendingBind> _binds = new();
    private int _bindCounter;

    private sealed record PendingBind(string Variable, INamedTypeSymbol Type, SuilElement Element, SuilProperty Property, SuilBindValue Bind);

    private static readonly SymbolDisplayFormat NullableFormat = SymbolDisplayFormat.FullyQualifiedFormat
        .AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    private void EmitBindings()
    {
        foreach (var bind in _binds)
        {
            EmitBinding(bind, _bindCounter++);
        }
    }

    private void EmitBinding(PendingBind pending, int index)
    {
        var property = pending.Property;
        var member = FindWritableMember(pending.Type, property.Name, out var memberType);

        if (member == null || memberType == null)
        {
            if (FindMarkedMember(pending.Type, property.Name) || FindShorthand(pending.Type, property.Name))
            {
                throw new SuilGenerationException(SuilDiagnostics.BadValue, property.Position,
                    $"The property '{property.Name}' can't be bound: a binding needs a public property with a setter (not a shorthand, not a property of the styles that has no member)");
            }

            throw new SuilGenerationException(SuilDiagnostics.UnknownProperty, property.Position,
                pending.Type.ToDisplayString(), property.Name);
        }

        var element = new PendingHandlers(pending.Variable, pending.Type, pending.Element);
        var assigned = new SuilProperty(property.Name, pending.Bind.Values, false, property.Position);
        var expression = InHandlerContext(element, null, () => Convert(memberType, assigned, member.Name, pending.Type));

        var type = memberType.ToDisplayString(NullableFormat);
        var bind = $"__suilBind{index}";
        var last = $"__suilLast{index}";
        var has = $"__suilHas{index}";
        var token = $"__suilBindToken{index}";

        _bindFields.AppendLine($"        private global::System.Action? {bind};");
        _bindFields.AppendLine($"        private {type} {last} = default!;");
        _bindFields.AppendLine($"        private bool {has};");

        _body.AppendLine($"            {bind} = () =>");
        _body.AppendLine("            {");
        _body.AppendLine($"                {type} __v = {expression};");
        _body.AppendLine($"                if (!{has} || !global::System.Collections.Generic.EqualityComparer<{type}>.Default.Equals({last}, __v))");
        _body.AppendLine("                {");
        _body.AppendLine($"                    {has} = true;");
        _body.AppendLine($"                    {last} = __v;");
        _body.AppendLine($"                    {pending.Variable}.{member.Name} = __v;");
        _body.AppendLine("                }");
        _body.AppendLine("            };");

        if (pending.Bind.Mode == SuilBindMode.Event)
        {
            var (owner, evt, delegateType, argsType) = ResolveEvent(pending.Bind.Scope, pending.Bind.Name!, pending.Bind.Event!, pending.Bind.Position);

            var call = new StringBuilder();
            call.AppendLine($"                {bind}!();");
            AddSourceSubscription(pending.Bind.Scope, owner, evt, delegateType, argsType, call, $"{bind}!();");
            return;
        }

        var period = pending.Bind.Mode == SuilBindMode.Every
            ? pending.Bind.Seconds.ToString("R", CultureInfo.InvariantCulture) + "d"
            : "0d";

        _bindFields.AppendLine($"        private object? {token};");
        _hookCount++;

        _subscribe.AppendLine($"            if ({token} == null)");
        _subscribe.AppendLine("            {");
        _subscribe.AppendLine($"                {bind}!();");
        _subscribe.AppendLine($"                {token} = global::Content.Suil.SuilBindings.Add({bind}!, {period});");
        _subscribe.AppendLine("            }");
        _subscribe.AppendLine();

        _unsubscribe.AppendLine("            global::Content.Suil.SuilBindings.Remove(" + token + ");");
        _unsubscribe.AppendLine($"            {token} = null;");
        _unsubscribe.AppendLine();
    }

    /// <summary>
    /// Works out an expression of values the way a handler does: the elements of the view are read when it runs.
    /// </summary>
    private string InHandlerContext(PendingHandlers pending, ITypeSymbol? argsType, Func<string> work)
    {
        _boolContext = node => BoolOf(node, pending, argsType);
        _numContext = node => NumOf(node, pending, argsType);
        _handlerElement = pending;
        _handlerArguments = argsType;

        try
        {
            return work();
        }
        finally
        {
            _boolContext = null;
            _numContext = null;
            _handlerElement = null;
            _handlerArguments = null;
        }
    }

    private static StringBuilder Indent(StringBuilder text)
    {
        var result = new StringBuilder();

        foreach (var line in text.ToString().Split('\n'))
        {
            var trimmed = line.TrimEnd('\r');

            if (trimmed.Length > 0)
                result.Append("    ").AppendLine(trimmed);
        }

        return result;
    }

    /// <summary>
    /// The public instance event of the class, or of its bases, with this exact name.
    /// </summary>
    private static IEventSymbol? FindClassEvent(INamedTypeSymbol type, string name)
    {
        for (INamedTypeSymbol? current = type; current != null; current = current.BaseType)
        {
            foreach (var member in current.GetMembers(name))
            {
                if (member is IEventSymbol { IsStatic: false, DeclaredAccessibility: Accessibility.Public } evt)
                    return evt;
            }
        }

        return null;
    }

    /// <summary>
    /// The hooks that subscribe and unsubscribe the view. The class may override <c>EnteredTree()</c> and
    /// <c>ExitedTree()</c> itself; then it calls <c>SuilEnteredTree()</c> and <c>SuilExitedTree()</c> from them.
    /// </summary>
    private void WriteSubscriptionMembers(StringBuilder sb)
    {
        sb.Append(_subscriptionFields);
        sb.Append(_bindFields);
        sb.AppendLine();
        sb.AppendLine("        private void SuilEnteredTree()");
        sb.AppendLine("        {");
        sb.Append(_subscribe);
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        private void SuilExitedTree()");
        sb.AppendLine("        {");
        sb.Append(_unsubscribe);
        sb.AppendLine("        }");
        sb.AppendLine();

        var entered = OverrideOf(viewClass, "EnteredTree");
        var exited = OverrideOf(viewClass, "ExitedTree");

        if (entered != null || exited != null)
        {
            // The class has its own; it has to pass the calls on, which is checked as far as the text of it goes.
            if (!CallsHook(entered, "SuilEnteredTree"))
                throw new SuilGenerationException(SuilDiagnostics.TreeHookTaken, _firstSubscription, viewClass.ToDisplayString(), "EnteredTree", "SuilEnteredTree");

            if (!CallsHook(exited, "SuilExitedTree"))
                throw new SuilGenerationException(SuilDiagnostics.TreeHookTaken, _firstSubscription, viewClass.ToDisplayString(), "ExitedTree", "SuilExitedTree");

            return;
        }

        sb.AppendLine("        protected override void EnteredTree()");
        sb.AppendLine("        {");
        sb.AppendLine("            base.EnteredTree();");
        sb.AppendLine("            SuilEnteredTree();");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        protected override void ExitedTree()");
        sb.AppendLine("        {");
        sb.AppendLine("            base.ExitedTree();");
        sb.AppendLine("            SuilExitedTree();");
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    private static IMethodSymbol? OverrideOf(INamedTypeSymbol type, string name)
        => type.GetMembers(name).OfType<IMethodSymbol>().FirstOrDefault(m => m.IsOverride && m.Parameters.Length == 0);

    private static bool CallsHook(IMethodSymbol? method, string hook)
    {
        // a hook the class doesn't override is the generated one, which calls it
        if (method == null)
            return true;

        foreach (var reference in method.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax().ToString().Contains(hook + "("))
                return true;
        }

        return false;
    }
}
