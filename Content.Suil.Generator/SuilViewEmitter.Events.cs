using System.Text;
using Microsoft.CodeAnalysis;

namespace Content.Suil.Generator;

/// <summary>
/// The handlers of the events: <c>on-pressed =&gt; @OnSubmit</c> and the blocks of statements become lambdas that are
/// subscribed in <c>InitializeComponent()</c> after the whole tree is built.
/// </summary>
internal sealed partial class SuilViewEmitter
{
    private readonly List<PendingHandlers> _handlers = new();
    private readonly Dictionary<string, (string Member, INamedTypeSymbol Type)> _fieldTypes = new();
    private string? _rootId;
    private int _handlerCounter;

    // How a bool expression becomes C# in the handler that is being written; null outside of one.
    private Func<SuilBoolNode, string>? _boolContext;

    private sealed record PendingHandlers(string Variable, INamedTypeSymbol Type, SuilElement Element);

    private void EmitHandlers()
    {
        foreach (var pending in _handlers)
        {
            foreach (var handler in pending.Element.Events)
            {
                EmitHandler(pending, handler);
            }
        }
    }

    private void EmitHandler(PendingHandlers pending, SuilEventHandler handler)
    {
        var evt = FindEvent(pending.Type, handler.Event);

        if (evt == null)
        {
            throw new SuilGenerationException(SuilDiagnostics.UnknownEvent, handler.Position,
                pending.Type.ToDisplayString(), handler.Event);
        }

        var delegateType = evt.Type as INamedTypeSymbol;
        var invoke = delegateType?.DelegateInvokeMethod;

        if (delegateType == null || invoke == null || !invoke.ReturnsVoid || invoke.Parameters.Length > 1)
        {
            throw new SuilGenerationException(SuilDiagnostics.UnsupportedEvent, handler.Position,
                evt.Name, pending.Type.ToDisplayString(), evt.Type.ToDisplayString());
        }

        var argsType = invoke.Parameters.Length == 1 ? invoke.Parameters[0].Type : null;
        var name = $"__h{_handlerCounter++}";
        var delegateName = delegateType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        var lambda = new StringBuilder();

        _boolContext = node => BoolOf(node, pending, argsType);

        foreach (var statement in handler.Statements)
        {
            EmitStatement(lambda, "                ", statement, pending, argsType);
        }

        _boolContext = null;

        _body.AppendLine($"            {delegateName} {name} = {(argsType == null ? "()" : "__args")} =>");
        _body.AppendLine("            {");
        _body.Append(lambda);
        _body.AppendLine("            };");
        _body.AppendLine($"            {pending.Variable}.{evt.Name} += {name};");
        _body.AppendLine($"            __unsub.Add(() => {pending.Variable}.{evt.Name} -= {name});");
    }

    /// <summary>
    /// The public instance event whose name in kebab-case is the one written in the file, of the type or of its bases.
    /// </summary>
    private static IEventSymbol? FindEvent(INamedTypeSymbol type, string name)
    {
        for (INamedTypeSymbol? current = type; current != null; current = current.BaseType)
        {
            foreach (var member in current.GetMembers())
            {
                if (member is IEventSymbol { IsStatic: false, DeclaredAccessibility: Accessibility.Public } evt
                    && SuilNaming.ToKebab(evt.Name) == name)
                {
                    return evt;
                }
            }
        }

        return null;
    }

    private void EmitStatement(
        StringBuilder sb,
        string indent,
        ISuilStatement statement,
        PendingHandlers pending,
        ITypeSymbol? argsType)
    {
        switch (statement)
        {
            case SuilCallStatement call:
                sb.AppendLine($"{indent}{CallOf(call, pending, argsType)}");
                break;
            case SuilTargetStatement target:
            {
                var (expression, type) = ResolveTarget(target.Target, pending);

                foreach (var item in target.Items)
                {
                    switch (item)
                    {
                        case SuilPropertyItem property:
                            sb.AppendLine($"{indent}{AssignmentOf(expression, type, property.Property)}");
                            break;
                        case SuilStyleItem style:
                            EmitStyle(sb, indent, expression, type, style, pending, argsType);
                            break;
                    }
                }

                break;
            }
        }
    }

    /// <summary>
    /// <c>this.OnSubmit(__args);</c>: the method must return nothing, and take nothing or something the arguments of the
    /// event can be passed to.
    /// </summary>
    private string CallOf(SuilCallStatement call, PendingHandlers pending, ITypeSymbol? argsType)
    {
        var method = FindHandlerMethod(call.Method);

        if (method == null)
        {
            throw new SuilGenerationException(SuilDiagnostics.UnknownMethod, call.Position,
                viewClass.ToDisplayString(), call.Method);
        }

        if (method.Parameters.Length == 0)
            return $"this.{method.Name}();";

        var parameter = method.Parameters[0].Type;

        if (argsType == null || !compilation.ClassifyCommonConversion(argsType, parameter).IsImplicit)
        {
            throw new SuilGenerationException(SuilDiagnostics.BadMethodSignature, call.Position,
                viewClass.ToDisplayString(), method.Name,
                argsType == null ? "an event without arguments" : argsType.ToDisplayString(),
                argsType == null ? "nothing" : argsType.ToDisplayString());
        }

        return $"this.{method.Name}(__args);";
    }

    /// <summary>
    /// A method of the class (or of its base classes, if the class can reach it) that a handler can call.
    /// </summary>
    private IMethodSymbol? FindHandlerMethod(string name)
    {
        for (INamedTypeSymbol? current = viewClass; current != null; current = current.BaseType)
        {
            foreach (var member in current.GetMembers(name))
            {
                if (member is IMethodSymbol method && IsHandlerMethod(method)
                    && compilation.IsSymbolAccessibleWithin(method, viewClass))
                {
                    return method;
                }
            }
        }

        return null;
    }

    private static bool IsHandlerMethod(IMethodSymbol method)
        => method is { MethodKind: MethodKind.Ordinary, IsGenericMethod: false, ReturnsVoid: true }
           && method.Parameters.Length <= 1
           && method.Parameters.All(p => p.RefKind == RefKind.None);

    // ---- Targets ----

    /// <summary>
    /// The C# expression of the element a statement works on and its type.
    /// </summary>
    private (string Expression, INamedTypeSymbol Type) ResolveTarget(SuilTarget target, PendingHandlers pending)
    {
        if (target.IsSender)
            return (pending.Variable, pending.Type);

        var id = target.Id!.ToString();
        string expression;
        INamedTypeSymbol type;

        if (_fieldTypes.TryGetValue(id, out var field))
        {
            expression = $"this.{field.Member}";
            type = field.Type;
        }
        else if (id == _rootId)
        {
            expression = "this";
            type = viewClass;
        }
        else
        {
            throw new SuilGenerationException(SuilDiagnostics.UnknownTarget, target.Position,
                $"There is no element with the id '{id}' in this view");
        }

        foreach (var segment in target.Path)
        {
            var name = SuilNaming.ToPascal(segment);
            var member = FindPublicMember(type, name, out var memberType);

            if (member != null && memberType is INamedTypeSymbol named)
            {
                expression += "." + member.Name;
                type = named;
                continue;
            }

            // a view of this project: its fields are not in the code yet, but they are in its file
            if (fieldsOfView(type) is { } fields && fields.TryGetValue(segment, out var viewField) && viewField.Access == SuilAccess.Public)
            {
                expression += "." + viewField.Member;
                type = viewField.Type;
                continue;
            }

            throw new SuilGenerationException(SuilDiagnostics.UnknownTarget, target.Position,
                $"'{type.ToDisplayString()}' has no public field '{name}' ('{segment}' in the path); only a public element ('+') of a view can be reached");
        }

        return (expression, type);
    }

    private static ISymbol? FindPublicMember(INamedTypeSymbol type, string name, out ITypeSymbol? memberType)
    {
        for (INamedTypeSymbol? current = type; current != null; current = current.BaseType)
        {
            foreach (var member in current.GetMembers(name))
            {
                switch (member)
                {
                    case IFieldSymbol { IsStatic: false, DeclaredAccessibility: Accessibility.Public } field:
                        memberType = field.Type;
                        return field;
                    case IPropertySymbol { IsStatic: false, IsIndexer: false, DeclaredAccessibility: Accessibility.Public } property:
                        memberType = property.Type;
                        return property;
                }
            }
        }

        memberType = null;
        return null;
    }

    // ---- Classes ----

    private void EmitStyle(
        StringBuilder sb,
        string indent,
        string expression,
        INamedTypeSymbol type,
        SuilStyleItem style,
        PendingHandlers pending,
        ITypeSymbol? argsType)
    {
        var isSuil = suilControl != null && SuilTagRegistry.Derives(type, suilControl);

        string Change(SuilStyleRef reference, bool on)
        {
            var name = Literal(reference.Name.ToString());

            if (reference.IsPseudo)
            {
                if (!isSuil)
                {
                    throw new SuilGenerationException(SuilDiagnostics.BadStatement, style.Position,
                        $"'{type.ToDisplayString()}' has no pseudo-classes: only a control of SUIL does (':{reference.Name}')");
                }

                return $"{expression}.SetSuilPseudoStyleClass({name}, {(on ? "true" : "false")});";
            }

            return (isSuil, on) switch
            {
                (true, true) => $"{expression}.AddSuilStyleClass({name});",
                (true, false) => $"{expression}.RemoveSuilStyleClass({name});",
                (false, true) => $"{expression}.AddStyleClass({name});",
                _ => $"{expression}.RemoveStyleClass({name});",
            };
        }

        switch (style.Op)
        {
            case SuilStyleOp.Add:
            case SuilStyleOp.Remove:
                foreach (var reference in style.Refs)
                {
                    sb.AppendLine($"{indent}{Change(reference, style.Op == SuilStyleOp.Add)}");
                }

                break;
            default:
            {
                var expression2 = style.State!.Expression;

                if (expression2 is SuilBoolConst constant)
                {
                    foreach (var reference in style.Refs)
                    {
                        sb.AppendLine($"{indent}{Change(reference, constant.Value)}");
                    }

                    break;
                }

                sb.AppendLine($"{indent}if ({BoolOf(expression2, pending, argsType)})");
                sb.AppendLine($"{indent}{{");

                foreach (var reference in style.Refs)
                {
                    sb.AppendLine($"{indent}    {Change(reference, true)}");
                }

                sb.AppendLine($"{indent}}}");
                sb.AppendLine($"{indent}else");
                sb.AppendLine($"{indent}{{");

                foreach (var reference in style.Refs)
                {
                    sb.AppendLine($"{indent}    {Change(reference, false)}");
                }

                sb.AppendLine($"{indent}}}");
                break;
            }
        }
    }

    // ---- Bool expressions ----

    /// <summary>
    /// The C# expression of a bool expression of a handler: the elements and the arguments are read where the handler runs.
    /// </summary>
    private string BoolOf(SuilBoolNode node, PendingHandlers pending, ITypeSymbol? argsType)
    {
        switch (node)
        {
            case SuilBoolConst constant:
                return constant.Value ? "true" : "false";
            case SuilBoolNot not:
                return $"!({BoolOf(not.Operand, pending, argsType)})";
            case SuilBoolAnd and:
                return $"({BoolOf(and.Left, pending, argsType)} && {BoolOf(and.Right, pending, argsType)})";
            case SuilBoolOr or:
                return $"({BoolOf(or.Left, pending, argsType)} || {BoolOf(or.Right, pending, argsType)})";
            case SuilBoolArgs args:
                return $"__args.{ArgsMember(argsType, args.Member, args.Position)}";
            case SuilBoolReference reference:
            {
                var (expression, type) = ResolveTarget(reference.Target, pending);
                var member = FindReadableBool(type, reference.Property);

                if (member == null)
                {
                    throw new SuilGenerationException(SuilDiagnostics.BadStatement, reference.Target.Position,
                        $"'{type.ToDisplayString()}' has no public bool property '{reference.Property}' to read");
                }

                return $"{expression}.{member}";
            }
            default:
                throw new SuilGenerationException(SuilDiagnostics.BadStatement, default,
                    "a variable is left in a bool expression: it must hold true or false");
        }
    }

    /// <summary>
    /// The public bool property or field whose name in kebab-case is the one written, of the type or its bases.
    /// </summary>
    private static string? FindReadableBool(INamedTypeSymbol type, string written)
    {
        for (INamedTypeSymbol? current = type; current != null; current = current.BaseType)
        {
            foreach (var member in current.GetMembers())
            {
                if (member.IsStatic || member.DeclaredAccessibility != Accessibility.Public
                    || SuilNaming.ToKebab(member.Name) != written)
                {
                    continue;
                }

                var memberType = member switch
                {
                    IPropertySymbol { IsIndexer: false, GetMethod: { DeclaredAccessibility: Accessibility.Public } } p => p.Type,
                    IFieldSymbol f => f.Type,
                    _ => null,
                };

                if (memberType?.SpecialType == SpecialType.System_Boolean)
                    return member.Name;
            }
        }

        return null;
    }

    /// <summary>
    /// The bool member of the arguments of the event that <c>args.pressed</c> names.
    /// </summary>
    private static string ArgsMember(ITypeSymbol? argsType, string written, SuilPosition position)
    {
        if (argsType is not INamedTypeSymbol named)
        {
            throw new SuilGenerationException(SuilDiagnostics.BadStatement, position,
                $"'args.{written}': the event has no arguments");
        }

        for (INamedTypeSymbol? current = named; current != null; current = current.BaseType)
        {
            foreach (var member in current.GetMembers())
            {
                if (SuilNaming.ToKebab(member.Name) != written || member.IsStatic
                    || member.DeclaredAccessibility != Accessibility.Public)
                {
                    continue;
                }

                var type = member switch
                {
                    IPropertySymbol { IsIndexer: false, GetMethod: not null } p => p.Type,
                    IFieldSymbol f => f.Type,
                    _ => null,
                };

                if (type?.SpecialType == SpecialType.System_Boolean)
                    return member.Name;
            }
        }

        throw new SuilGenerationException(SuilDiagnostics.BadStatement, position,
            $"'{named.ToDisplayString()}' has no public bool member '{written}' for 'args.{written}'");
    }

    // ---- ISuilView: the methods ----

    /// <summary>
    /// <c>SuilTryGetMethod</c> and <c>SuilInvoke</c> of <c>ISuilView</c>: the methods the hot reload may bind to a handler
    /// of the new file, which the generator can't know from the old one. Every method of the class that a handler can
    /// call is in the switch.
    /// </summary>
    private void WriteMethodMembers(StringBuilder sb)
    {
        const string view = "global::Content.Suil.ISuilView";
        var methods = new List<IMethodSymbol>();
        var names = new HashSet<string>();

        foreach (var member in viewClass.GetMembers())
        {
            if (member is IMethodSymbol method && IsHandlerMethod(method) && method.Name != "InitializeComponent"
                && !method.IsStatic && names.Add(method.Name))
            {
                methods.Add(method);
            }
        }

        sb.AppendLine();
        sb.AppendLine($"        bool {view}.SuilTryGetMethod(string name, out global::System.Type? argumentType)");
        sb.AppendLine("        {");
        sb.AppendLine("            switch (name)");
        sb.AppendLine("            {");

        foreach (var method in methods)
        {
            var argument = method.Parameters.Length == 0
                ? "null"
                : $"typeof({method.Parameters[0].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)})";

            sb.AppendLine($"                case {Literal(method.Name)}:");
            sb.AppendLine($"                    argumentType = {argument};");
            sb.AppendLine("                    return true;");
        }

        sb.AppendLine("                default:");
        sb.AppendLine("                    argumentType = null;");
        sb.AppendLine("                    return false;");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine($"        void {view}.SuilInvoke(string name, object? argument)");
        sb.AppendLine("        {");
        sb.AppendLine("            switch (name)");
        sb.AppendLine("            {");

        foreach (var method in methods)
        {
            var call = method.Parameters.Length == 0
                ? $"this.{method.Name}();"
                : $"this.{method.Name}(({method.Parameters[0].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}) argument!);";

            sb.AppendLine($"                case {Literal(method.Name)}:");
            sb.AppendLine($"                    {call}");
            sb.AppendLine("                    break;");
        }

        sb.AppendLine("            }");
        sb.AppendLine("        }");
    }
}
