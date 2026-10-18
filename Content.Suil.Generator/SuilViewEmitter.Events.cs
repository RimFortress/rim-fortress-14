using System.Globalization;
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

    // The same for the number expressions.
    private Func<SuilNumNode, string>? _numContext;

    // The element of the handler that is being written and the type of the arguments of its event, for the values that read
    // a property of another element or a member of the arguments.
    private PendingHandlers? _handlerElement;
    private ITypeSymbol? _handlerArguments;

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
        _numContext = node => NumOf(node, pending, argsType);
        _handlerElement = pending;
        _handlerArguments = argsType;

        // A call without parentheses gets the arguments of the event only as the lone statement of a handler without a block.
        var autoArguments = !handler.IsBlock && handler.Statements.Count == 1;

        foreach (var statement in handler.Statements)
        {
            EmitStatement(lambda, "                ", statement, pending, argsType, autoArguments);
        }

        _boolContext = null;
        _numContext = null;
        _handlerElement = null;
        _handlerArguments = null;

        _body.AppendLine($"            {delegateName} {name} = {(argsType == null ? "()" : "__args")} =>");
        _body.AppendLine("            {");
        _body.Append(lambda);
        _body.AppendLine("            };");
        _body.AppendLine($"            {pending.Variable}.{evt.Name} += {name};");
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
        ITypeSymbol? argsType,
        bool autoArguments)
    {
        switch (statement)
        {
            case SuilCallStatement call:
                sb.AppendLine($"{indent}{CallOf(call, pending, argsType, autoArguments)}");
                break;
            case SuilIfStatement ifStatement:
                EmitIf(sb, indent, ifStatement, pending, argsType);
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
                        case SuilOperatorItem op:
                            sb.AppendLine($"{indent}{OperatorOf(expression, type, op, pending, argsType)}");
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
    /// <c>if (...) { } else if (...) { } else { }</c>. A condition that is a constant is worked out here: the branches that
    /// can't run are not written (and the ones after a true one), so the compiler has nothing to warn about.
    /// </summary>
    private void EmitIf(StringBuilder sb, string indent, SuilIfStatement statement, PendingHandlers pending, ITypeSymbol? argsType)
    {
        var branches = new List<SuilIfBranch>();
        var otherwise = statement.Else;

        foreach (var branch in statement.Branches)
        {
            if (branch.Condition is SuilBoolConst { Value: false })
                continue;

            if (branch.Condition is SuilBoolConst { Value: true })
            {
                otherwise = branch.Body;
                break;
            }

            branches.Add(branch);
        }

        void Body(List<ISuilStatement> statements, string bodyIndent)
        {
            foreach (var inner in statements)
            {
                EmitStatement(sb, bodyIndent, inner, pending, argsType, autoArguments: false);
            }
        }

        if (branches.Count == 0)
        {
            if (otherwise != null)
                Body(otherwise, indent);

            return;
        }

        for (var i = 0; i < branches.Count; i++)
        {
            sb.AppendLine($"{indent}{(i == 0 ? "if" : "else if")} ({BoolOf(branches[i].Condition, pending, argsType)})");
            sb.AppendLine($"{indent}{{");
            Body(branches[i].Body, indent + "    ");
            sb.AppendLine($"{indent}}}");
        }

        if (otherwise != null)
        {
            sb.AppendLine($"{indent}else");
            sb.AppendLine($"{indent}{{");
            Body(otherwise, indent + "    ");
            sb.AppendLine($"{indent}}}");
        }
    }

    /// <summary>
    /// The value of a property of another element or of a member of the arguments of the event, as a C# expression of the
    /// type the value is assigned to: <c>#name-edit.text</c>, <c>args.text</c>.
    /// </summary>
    private string ReadOf(SuilBoolNode reference, ITypeSymbol target, SuilPosition position)
    {
        var pending = _handlerElement!;

        switch (reference)
        {
            case SuilBoolReference element:
            {
                var (expression, type) = ResolveTarget(element.Target, pending);
                var (read, readType) = MemberPath(expression, type, new List<string> { element.Property }, element.Target.Position, "the element");
                return Checked(read, readType, target, element.Target.Position);
            }
            case SuilBoolArgs args:
            {
                if (_handlerArguments == null)
                {
                    throw new SuilGenerationException(SuilDiagnostics.BadStatement, args.Position,
                        "'args' is used in an event that gives no arguments");
                }

                var (read, readType) = MemberPath("__args", _handlerArguments, new List<string> { args.Member }, args.Position, "args");
                return Checked(read, readType, target, args.Position);
            }
            default:
                throw new SuilGenerationException(SuilDiagnostics.BadStatement, position, "unknown value");
        }
    }

    private static readonly SymbolDisplayFormat TypeFormat = SymbolDisplayFormat.FullyQualifiedFormat
        .WithMiscellaneousOptions(
            SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions
            & ~SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    // Set while the arguments of a call are written: a value there is a C# value of the parameter, not a property.
    private bool _argumentMode;

    /// <summary>
    /// The C# statement of a call: <c>this.OnSubmit(__args);</c>, <c>this.OnSubmit(a, b);</c> or the same on a controller
    /// or a system.
    /// </summary>
    private string CallOf(SuilCallStatement call, PendingHandlers pending, ITypeSymbol? argsType, bool autoArguments)
        => PrepareCall(call, pending, argsType, autoArguments).Invocation + ";";

    /// <summary>
    /// The C# call (without the semicolon) and the method it is of.
    /// </summary>
    private (string Invocation, IMethodSymbol Method) PrepareCall(
        SuilCallStatement call,
        PendingHandlers pending,
        ITypeSymbol? argsType,
        bool autoArguments)
    {
        INamedTypeSymbol owner;
        string receiver;
        string description;

        if (call.Scope == SuilCallScope.View)
        {
            owner = viewClass;
            receiver = "this";
            description = viewClass.ToDisplayString();
        }
        else
        {
            owner = SuilCallClasses.Find(compilation, call.Scope, call.Name!, out var candidates)
                    ?? throw new SuilGenerationException(SuilDiagnostics.UnknownCallClass, call.Position,
                        call.Scope == SuilCallScope.Controller ? "Controllers" : "Systems",
                        call.Name!,
                        candidates.Count == 0
                            ? "there is no such " + (call.Scope == SuilCallScope.Controller ? "UI controller (a class named NameUiController)" : "system (a class named NameSystem)")
                            : "it can be " + string.Join(", ", candidates.Select(c => c.ToDisplayString()).OrderBy(n => n, StringComparer.Ordinal)));

            receiver = call.Scope == SuilCallScope.Controller
                ? $"global::Robust.Shared.IoC.IoCManager.Resolve<global::Robust.Client.UserInterface.IUserInterfaceManager>().GetUIController<{owner.ToDisplayString(TypeFormat)}>()"
                : $"global::Robust.Shared.IoC.IoCManager.Resolve<global::Robust.Shared.GameObjects.IEntityManager>().System<{owner.ToDisplayString(TypeFormat)}>()";
            description = owner.ToDisplayString();
        }

        var candidatesOfName = MethodsNamed(owner, call.Method, call.Scope == SuilCallScope.View);

        if (candidatesOfName.Count == 0)
        {
            throw new SuilGenerationException(SuilDiagnostics.UnknownMethod, call.Position,
                description, call.Method);
        }

        var (method, arguments) = ChooseOverload(candidatesOfName, call, pending, argsType, autoArguments, description);

        return ($"{receiver}.{method.Name}({string.Join(", ", arguments)})", method);
    }

    /// <summary>
    /// The overload a call means and what is passed to it. Without parentheses it is the one that takes nothing, or, for
    /// the lone statement of a handler, the one that takes the arguments of the event; with them, the first one the
    /// arguments fit.
    /// </summary>
    private (IMethodSymbol Method, List<string> Arguments) ChooseOverload(
        IReadOnlyList<IMethodSymbol> candidates,
        SuilCallStatement call,
        PendingHandlers pending,
        ITypeSymbol? argsType,
        bool autoArguments,
        string description)
    {
        if (call.Arguments == null)
        {
            var parameterless = candidates.FirstOrDefault(m => m.Parameters.All(p => p.IsOptional));

            if (parameterless != null)
                return (parameterless, new List<string>());

            if (autoArguments && argsType != null)
            {
                var takesEvent = candidates.FirstOrDefault(m =>
                    m.Parameters.Length == 1 && compilation.ClassifyCommonConversion(argsType, m.Parameters[0].Type).IsImplicit);

                if (takesEvent != null)
                    return (takesEvent, new List<string> { "__args" });
            }

            var first = candidates[0];
            var hint = first.Parameters.Length == 0
                ? string.Empty
                : autoArguments
                    ? $"it takes {string.Join(", ", first.Parameters.Select(p => p.Type.ToDisplayString()))}; write '@{call.Method}(...)' with the arguments"
                    : $"it takes {string.Join(", ", first.Parameters.Select(p => p.Type.ToDisplayString()))}; write '@{call.Method}(args)' to pass the arguments of the event";

            throw new SuilGenerationException(SuilDiagnostics.BadMethodSignature, call.Position,
                description, call.Method, "a call without arguments", hint);
        }

        SuilGenerationException? failure = null;

        foreach (var method in candidates)
        {
            var required = method.Parameters.Count(p => !p.IsOptional);

            if (call.Arguments.Count < required || call.Arguments.Count > method.Parameters.Length)
                continue;

            try
            {
                var arguments = new List<string>();

                for (var i = 0; i < call.Arguments.Count; i++)
                {
                    arguments.Add(ArgumentOf(call.Arguments[i], method.Parameters[i].Type, pending, argsType, i));
                }

                return (method, arguments);
            }
            catch (SuilGenerationException e)
            {
                failure ??= e;
            }
        }

        if (failure != null)
            throw failure;

        throw new SuilGenerationException(SuilDiagnostics.BadMethodSignature, call.Position,
            description, call.Method, $"{call.Arguments.Count} arguments",
            "it takes " + string.Join(" or ", candidates.Select(m => m.Parameters.Length.ToString()).Distinct()) + " arguments");
    }

    /// <summary>
    /// The C# expression of an argument of a call, of the type of the parameter.
    /// </summary>
    private string ArgumentOf(SuilCallArgument argument, ITypeSymbol parameter, PendingHandlers pending, ITypeSymbol? argsType, int index)
    {
        switch (argument)
        {
            case SuilEventArgument evt:
            {
                if (argsType == null)
                {
                    throw new SuilGenerationException(SuilDiagnostics.BadStatement, evt.Position,
                        "'args' is used in an event that gives no arguments");
                }

                var (expression, type) = MemberPath("__args", argsType, evt.Path, evt.Position, "args");
                return Checked(expression, type, parameter, evt.Position);
            }
            case SuilTargetArgument target:
            {
                var (expression, type) = ResolveTarget(target.Target, pending);
                ITypeSymbol finalType = type;

                if (target.Property != null)
                    (expression, finalType) = MemberPath(expression, type, new List<string> { target.Property }, target.Position, "the element");

                return Checked(expression, finalType, parameter, target.Position);
            }
            case SuilValueArgument value:
            {
                var property = new SuilProperty("argument", value.Values, false, value.Position);
                _argumentMode = true;

                try
                {
                    return Convert(parameter, property, $"argument {index + 1}", viewClass);
                }
                finally
                {
                    _argumentMode = false;
                }
            }
            default:
                throw new SuilGenerationException(SuilDiagnostics.BadStatement, argument.Position, "unknown argument");
        }
    }

    /// <summary>
    /// <c>__args.New</c>: the members after a name, as written in kebab-case.
    /// </summary>
    private static (string Expression, ITypeSymbol Type) MemberPath(
        string expression,
        ITypeSymbol type,
        IReadOnlyList<string> path,
        SuilPosition position,
        string what)
    {
        foreach (var segment in path)
        {
            var name = SuilNaming.ToPascal(segment);
            ITypeSymbol? memberType = null;
            var member = type is INamedTypeSymbol named ? FindPublicMember(named, name, out memberType) : null;

            if (member == null || memberType == null || !IsReadable(member))
            {
                throw new SuilGenerationException(SuilDiagnostics.BadStatement, position,
                    $"{what} ('{type.ToDisplayString()}') has no public member '{name}' to read ('{segment}')");
            }

            expression += "." + member.Name;
            type = memberType;
        }

        return (expression, type);
    }

    /// <summary>
    /// The expression, if a value of its type can be passed as the parameter.
    /// </summary>
    private string Checked(string expression, ITypeSymbol type, ITypeSymbol parameter, SuilPosition position)
    {
        var conversion = compilation.ClassifyCommonConversion(type, parameter);

        if (conversion.IsImplicit)
            return expression;

        // a cast that C# allows is made by itself; anything else is a mistake
        if (conversion.Exists || IsNumeric(type.SpecialType, out _) && IsNumeric(parameter.SpecialType, out _))
            return $"({parameter.ToDisplayString(TypeFormat)}){expression}";

        throw new SuilGenerationException(SuilDiagnostics.BadStatement, position,
            $"{type.ToDisplayString()} can't be passed as {parameter.ToDisplayString()}");
    }

    /// <summary>
    /// The methods of a class and of its bases that a call can make. The members of the base class of every control
    /// (<c>Control</c>) and of <c>object</c> are not among them: a file doesn't call <c>AddChild</c>.
    /// </summary>
    private IReadOnlyList<IMethodSymbol> MethodsNamed(INamedTypeSymbol type, string name, bool isView)
    {
        var result = new List<IMethodSymbol>();

        for (INamedTypeSymbol? current = type; current != null; current = current.BaseType)
        {
            if (IsControlRoot(current, isView))
                break;

            foreach (var member in current.GetMembers(name))
            {
                if (member is IMethodSymbol method && IsCallable(method) && compilation.IsSymbolAccessibleWithin(method, viewClass))
                    result.Add(method);
            }
        }

        return result;
    }

    /// <summary>
    /// The classes whose methods are not called from a file: <c>object</c> and, for a view, the base classes of every
    /// control (<c>Control</c> and <c>UiElement</c>).
    /// </summary>
    private static bool IsControlRoot(INamedTypeSymbol type, bool isView)
        => type.SpecialType == SpecialType.System_Object
           || isView && type.ToDisplayString() is "Robust.Client.UserInterface.Control" or "Content.Client._RF.Suil.UiElement";

    private static bool IsCallable(IMethodSymbol method)
        => method is { MethodKind: MethodKind.Ordinary, IsGenericMethod: false, IsStatic: false }
           && method.Parameters.Length <= 8
           && method.Parameters.All(p => p.RefKind == RefKind.None);

    // ---- Targets ----

    /// <summary>
    /// The C# expression of the element a statement works on and its type.
    /// </summary>
    private (string Expression, INamedTypeSymbol Type) ResolveTarget(SuilTarget target, PendingHandlers pending)
    {
        if (target.IsView)
            return ("this", viewClass);

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

    // ---- Number expressions ----

    /// <summary>
    /// <c>min-width +: 10</c> is <c>x.MinWidth = (float)((double)x.MinWidth + (10d));</c>
    /// </summary>
    private string OperatorOf(string target, INamedTypeSymbol type, SuilOperatorItem item, PendingHandlers pending, ITypeSymbol? argsType)
    {
        var member = FindWritableMember(type, item.Property, out var memberType);

        if (member == null || memberType == null)
        {
            throw new SuilGenerationException(SuilDiagnostics.UnknownProperty, item.Position,
                type.ToDisplayString(), item.Property);
        }

        if (!IsNumeric(memberType.SpecialType, out _) || !IsReadable(member))
        {
            throw new SuilGenerationException(SuilDiagnostics.BadStatement, item.Position,
                $"'{item.Property}' of '{type.ToDisplayString()}' is {memberType.ToDisplayString()}: '{item.Operator}:' works on a numeric property that can be read and written");
        }

        var current = $"(double){target}.{member.Name}";
        var value = $"{current} {item.Operator} ({NumOf(item.Operand, pending, argsType)})";

        return $"{target}.{member.Name} = {ToNumeric(memberType.SpecialType, value)};";
    }

    private static bool IsReadable(ISymbol member)
        => member is IFieldSymbol || member is IPropertySymbol { GetMethod: { DeclaredAccessibility: Accessibility.Public } };

    /// <summary>
    /// A number of C# (a <c>double</c> expression) in the numeric type of a field.
    /// </summary>
    private static string ToNumeric(SpecialType type, string expression)
    {
        switch (type)
        {
            case SpecialType.System_Double:
                return $"({expression})";
            case SpecialType.System_Single:
                return $"(float)({expression})";
        }

        var (clr, keyword) = type switch
        {
            SpecialType.System_SByte => ("SByte", "sbyte"),
            SpecialType.System_Byte => ("Byte", "byte"),
            SpecialType.System_Int16 => ("Int16", "short"),
            SpecialType.System_UInt16 => ("UInt16", "ushort"),
            SpecialType.System_Int32 => ("Int32", "int"),
            SpecialType.System_UInt32 => ("UInt32", "uint"),
            SpecialType.System_Int64 => ("Int64", "long"),
            _ => ("UInt64", "ulong"),
        };

        return $"({keyword}) global::Content.Suil.SuilMath.ToInteger({expression}, global::System.{clr}.MinValue, global::System.{clr}.MaxValue)";
    }

    /// <summary>
    /// The C# expression (of the type double) of a number expression of a handler.
    /// </summary>
    private string NumOf(SuilNumNode node, PendingHandlers pending, ITypeSymbol? argsType)
    {
        switch (node)
        {
            case SuilNumConst constant:
            {
                var text = constant.Value.ToString("R", CultureInfo.InvariantCulture) + "d";
                return constant.Value < 0 ? $"({text})" : text;
            }
            case SuilNumNegate negate:
                return $"(-({NumOf(negate.Operand, pending, argsType)}))";
            case SuilNumBinary binary:
                return $"({NumOf(binary.Left, pending, argsType)} {binary.Operator} {NumOf(binary.Right, pending, argsType)})";
            case SuilNumFunction function:
            {
                var arguments = function.Arguments.Select(a => NumOf(a, pending, argsType)).ToList();

                switch (function.Name)
                {
                    case "abs":
                        return $"global::System.Math.Abs({arguments[0]})";
                    case "clamp":
                        return $"global::System.Math.Max({arguments[0]}, global::System.Math.Min({arguments[1]}, {arguments[2]}))";
                    default:
                    {
                        var method = function.Name == "min" ? "Min" : "Max";
                        var result = arguments[0];

                        for (var i = 1; i < arguments.Count; i++)
                        {
                            result = $"global::System.Math.{method}({result}, {arguments[i]})";
                        }

                        return result;
                    }
                }
            }
            case SuilNumReference reference:
            {
                var (expression, type) = ResolveTarget(reference.Target, pending);
                var member = FindReadableNumber(type, reference.Property);

                if (member == null)
                {
                    throw new SuilGenerationException(SuilDiagnostics.BadStatement, reference.Target.Position,
                        $"'{type.ToDisplayString()}' has no public numeric property '{reference.Property}' to read");
                }

                return $"((double){expression}.{member})";
            }
            default:
                throw new SuilGenerationException(SuilDiagnostics.BadStatement, default,
                    "a variable is left in a number expression: it must hold a number");
        }
    }

    private static string? FindReadableNumber(INamedTypeSymbol type, string written)
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

                if (memberType != null && IsNumeric(memberType.SpecialType, out _))
                    return member.Name;
            }
        }

        return null;
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
            case SuilBoolCompare compare:
                return CompareOf(compare, pending, argsType);
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
}
