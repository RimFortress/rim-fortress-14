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

    private bool HasSubscriptions => _subscriptionCounter > 0;

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
        var scopeName = subscription.Scope == SuilCallScope.Controller ? "Controllers" : "Systems";
        var owner = SuilCallClasses.Find(compilation, subscription.Scope, subscription.Name, out var candidates)
                    ?? throw new SuilGenerationException(SuilDiagnostics.UnknownCallClass, subscription.Position,
                        scopeName,
                        subscription.Name,
                        candidates.Count == 0
                            ? "there is no such " + (subscription.Scope == SuilCallScope.Controller ? "UI controller (a class named NameUiController)" : "system (a class named NameSystem)")
                            : "it can be " + string.Join(", ", candidates.Select(c => c.ToDisplayString()).OrderBy(n => n, StringComparer.Ordinal)));

        var evt = FindClassEvent(owner, subscription.Event)
                  ?? throw new SuilGenerationException(SuilDiagnostics.UnknownClassEvent, subscription.Position,
                      owner.ToDisplayString(), subscription.Event);

        var delegateType = evt.Type as INamedTypeSymbol;
        var invoke = delegateType?.DelegateInvokeMethod;

        if (delegateType == null || invoke == null || !invoke.ReturnsVoid || invoke.Parameters.Length > 1)
        {
            throw new SuilGenerationException(SuilDiagnostics.UnsupportedClassEvent, subscription.Position,
                evt.Name, owner.ToDisplayString(), evt.Type.ToDisplayString());
        }

        var argsType = invoke.Parameters.Length == 1 ? invoke.Parameters[0].Type : null;
        var index = _subscriptionCounter++;
        var delegateName = delegateType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var ownerName = owner.ToDisplayString(TypeFormat);
        var source = $"__suilSource{index}";
        var handlerField = $"__suilOn{index}";

        var lambda = StatementsOf(pending, subscription.Handler, argsType);

        var resolve = subscription.Scope == SuilCallScope.Controller
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
