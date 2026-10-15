using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Content.Suil.Generator;

/// <summary>
/// Raised for the problems found while the code of a view is written: they become diagnostics.
/// </summary>
internal sealed class SuilGenerationException(DiagnosticDescriptor descriptor, SuilPosition position, params object[] args)
    : Exception
{
    public DiagnosticDescriptor Descriptor { get; } = descriptor;

    public SuilPosition Position { get; } = position;

    public object[] Args { get; } = args;
}

/// <summary>
/// A field of a view as its file describes it: the generator of another view in the same project can't see the code
/// that is generated for this one, so the path <c>#panel/header</c> asks the file.
/// </summary>
internal sealed record ViewField(string Member, SuilAccess Access, INamedTypeSymbol Type);

/// <summary>
/// Writes the partial class of a view from the expanded tree: a field for every element with an id and
/// <c>InitializeComponent()</c> that builds the elements, sets the classes, ids and properties and adds the children.
/// </summary>
internal sealed partial class SuilViewEmitter(
    Compilation compilation,
    SuilTagRegistry tags,
    INamedTypeSymbol? suilControl,
    INamedTypeSymbol viewClass,
    Func<INamedTypeSymbol, IReadOnlyDictionary<string, ViewField>?> fieldsOfView)
{
    private readonly StringBuilder _body = new();
    private readonly List<(string Id, string Name, string Type, string Access)> _fields = new();
    private readonly HashSet<string> _ids = new();
    private int _counter;

    public string Emit(SuilElement root, string viewPath)
    {
        var rootType = ResolveType(root);

        if (!SuilTagRegistry.Derives(viewClass, rootType))
        {
            throw new SuilGenerationException(SuilDiagnostics.RootTypeMismatch, root.Position,
                viewClass.ToDisplayString(), rootType.ToDisplayString(), root.Tag ?? "_");
        }

        _rootId = root.Id?.ToString();

        EmitElement(root, "this", rootType);

        // The handlers come last: they name elements of the whole view, which have their fields only now.
        EmitHandlers();

        return Wrap(viewPath);
    }

    private INamedTypeSymbol ResolveType(SuilElement element)
    {
        var tag = element.Tag ?? SuilTagRegistry.UntypedTag;

        if (tags.TryResolve(tag, out var type, out var ambiguous))
            return type!;

        if (ambiguous.Count > 0)
        {
            throw new SuilGenerationException(SuilDiagnostics.AmbiguousTag, element.Position, tag,
                string.Join(", ", ambiguous.Select(t => t.ToDisplayString())));
        }

        throw new SuilGenerationException(SuilDiagnostics.UnknownTag, element.Position, tag);
    }

    private void EmitElement(SuilElement element, string variable, INamedTypeSymbol type)
    {
        var isSuil = suilControl != null && SuilTagRegistry.Derives(type, suilControl);

        if (element.Id != null)
        {
            var id = element.Id.ToString();
            _body.AppendLine(isSuil
                ? $"            {variable}.SuilStyleIdentifier = {Literal(id)};"
                : $"            {variable}.Name = {Literal(id)};");
        }

        foreach (var cls in element.Classes)
        {
            _body.AppendLine(isSuil
                ? $"            {variable}.AddSuilStyleClass({Literal(cls.ToString())});"
                : $"            {variable}.AddStyleClass({Literal(cls.ToString())});");
        }

        var properties = element.Properties;

        foreach (var property in properties)
        {
            EmitProperty(variable, type, property);

            // The file assigned it, so the styles must leave it alone, unless it is soft: `?` before the value.
            // Only a control of SUIL is styled, an engine one has nothing to protect.
            if (isSuil && !property.IsSoft)
                _body.AppendLine($"            {variable}.SuilSetHard({Literal(property.Name)});");
        }

        if (element.Events.Count > 0)
            _handlers.Add(new PendingHandlers(variable, variable == "this" ? viewClass : type, element));

        foreach (var node in element.Children)
        {
            if (node is not SuilElement child)
                continue;

            var childType = ResolveType(child);
            var constructor = childType.InstanceConstructors.FirstOrDefault(c =>
                c.Parameters.Length == 0 && compilation.IsSymbolAccessibleWithin(c, viewClass.ContainingAssembly));

            if (constructor == null)
            {
                throw new SuilGenerationException(SuilDiagnostics.NoDefaultConstructor, child.Position,
                    childType.ToDisplayString(), child.Tag ?? "_");
            }

            var childVariable = $"__e{_counter++}";
            var typeName = childType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            _body.AppendLine($"            var {childVariable} = new {typeName}();");
            EmitElement(child, childVariable, childType);

            if (child.Id != null)
                AddField(child, childVariable, typeName, childType);

            _body.AppendLine($"            {variable}.AddChild({childVariable});");
        }
    }

    private void AddField(SuilElement element, string variable, string typeName, INamedTypeSymbol type)
    {
        var id = element.Id!.ToString();

        if (!_ids.Add(id))
            throw new SuilGenerationException(SuilDiagnostics.DuplicateId, element.Position, id);

        var name = SuilNaming.ToPascal(id);

        if (viewClass.GetMembers(name).Length > 0)
        {
            throw new SuilGenerationException(SuilDiagnostics.FieldClash, element.Position, id, name,
                viewClass.ToDisplayString());
        }

        var access = element.Access switch
        {
            SuilAccess.Public => "public",
            SuilAccess.Protected => "protected",
            _ => "private",
        };

        _fields.Add((id, name, typeName, access));
        _fieldTypes[id] = (name, type);
        _body.AppendLine($"            this.{name} = {variable};");
    }

    // ---- Properties ----

    private void EmitProperty(string variable, INamedTypeSymbol type, SuilProperty property)
        => _body.AppendLine($"            {AssignmentOf(variable, type, property)}");

    /// <summary>
    /// The C# statement that assigns a property of the file to a member of the target.
    /// </summary>
    private string AssignmentOf(string target, INamedTypeSymbol type, SuilProperty property)
    {
        var member = FindWritableMember(type, property.Name, out var memberType);

        if (member == null || memberType == null)
        {
            throw new SuilGenerationException(SuilDiagnostics.UnknownProperty, property.Position,
                type.ToDisplayString(), property.Name);
        }

        var value = Convert(memberType, property, member.Name);
        return $"{target}.{member.Name} = {value};";
    }

    /// <summary>
    /// Finds a public settable property or field whose name in kebab-case is the name of the SUIL property:
    /// <c>override-direction</c> is <c>OverrideDirection</c>, <c>ui-scale</c> is <c>UIScale</c>.
    /// </summary>
    private static ISymbol? FindWritableMember(INamedTypeSymbol type, string propertyName, out ITypeSymbol? memberType)
    {
        for (INamedTypeSymbol? current = type; current != null; current = current.BaseType)
        {
            foreach (var member in current.GetMembers())
            {
                if (SuilNaming.ToKebab(member.Name) != propertyName)
                    continue;

                switch (member)
                {
                    case IPropertySymbol { IsStatic: false, SetMethod: { DeclaredAccessibility: Accessibility.Public, IsInitOnly: false } } property:
                        memberType = property.Type;
                        return property;
                    case IFieldSymbol { IsStatic: false, IsReadOnly: false, IsConst: false, DeclaredAccessibility: Accessibility.Public } field:
                        memberType = field.Type;
                        return field;
                }
            }
        }

        memberType = null;
        return null;
    }

    /// <summary>
    /// Writes a C# expression of the given type for the values of a property.
    /// </summary>
    private string Convert(ITypeSymbol target, SuilProperty property, string memberName)
    {
        var values = property.Value;

        if (target is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
            target = nullable.TypeArguments[0];

        string Fail(string reason)
        {
            throw new SuilGenerationException(SuilDiagnostics.BadValue, property.Position,
                $"The property '{property.Name}' ({memberName}: {target.ToDisplayString()}): {reason}");
        }

        // a flag without a value
        if (values == null)
        {
            return target.SpecialType == SpecialType.System_Boolean
                ? "true"
                : Fail("it needs a value");
        }

        if (target.SpecialType == SpecialType.System_String)
        {
            if (values.Count == 1)
            {
                switch (values[0])
                {
                    case SuilString str:
                        return Literal(string.Concat(str.Parts.Select(p => p.Text)));
                    case SuilIdent ident:
                        return Literal(ident.Value);
                    case SuilLocalization loc:
                        return LocalizationCall(loc, property);
                }
            }

            return Fail("expected a string, an ident or a $localization key");
        }

        // `min-width: calc(#panel.min-width * 2)`, or `min-width: #panel.min-width`: read when the event is raised
        if (IsNumeric(target.SpecialType, out _) && _numContext != null)
        {
            if (values is [SuilNumValue numeric])
                return ToNumeric(target.SpecialType, _numContext(numeric.Root));

            // a lone reference is read as a bool by the parser; the property it is assigned to tells what it is
            if (values is [SuilBoolValue { Root: SuilBoolReference reference }])
                return ToNumeric(target.SpecialType, _numContext(new SuilNumReference(reference.Target, reference.Property)));
        }

        if (values.Any(v => v is SuilNumValue))
            return Fail("a number expression can only be assigned to a numeric property");

        if (values.Any(v => v is SuilBoolValue) && target.SpecialType != SpecialType.System_Boolean)
            return Fail("a bool expression can only be assigned to a bool property");

        if (target.SpecialType == SpecialType.System_Boolean)
        {
            if (values.Count == 1 && values[0] is SuilIdent { Value: "true" or "false" } boolean)
                return boolean.Value;

            // `!#menu.visible && args.pressed`: read when the event is raised
            if (values.Count == 1 && values[0] is SuilBoolValue expression && _boolContext != null)
                return _boolContext(expression.Root);

            return Fail("expected true or false, or a bool expression in a handler of an event");
        }

        if (IsNumeric(target.SpecialType, out var isIntegral))
        {
            if (values.Count == 1 && TryNumber(values[0], out var number, out var integer))
            {
                if (isIntegral && !integer)
                    return Fail("expected a whole number");

                return $"({CSharpKeyword(target.SpecialType)}){NumberLiteral(number, integer)}";
            }

            return Fail("expected a number, or a number expression in a handler of an event");
        }

        if (target.TypeKind == TypeKind.Enum)
            return EnumMember((INamedTypeSymbol) target, values, Fail);

        switch (target.ToDisplayString())
        {
            case "System.Numerics.Vector2":
            {
                var numbers = Numbers(values);
                if (numbers is { Count: 1 })
                    return $"new global::System.Numerics.Vector2({FloatLiteral(numbers[0])})";

                if (numbers is { Count: 2 })
                    return $"new global::System.Numerics.Vector2({FloatLiteral(numbers[0])}, {FloatLiteral(numbers[1])})";

                return Fail("expected one or two numbers");
            }
            case "Robust.Shared.Maths.Thickness":
            {
                // As in SUIL: one value is all sides, two are vertical and horizontal, four are top, right, bottom, left.
                var n = Numbers(values);
                var thickness = "new global::Robust.Shared.Maths.Thickness";

                if (n is { Count: 1 })
                    return $"{thickness}({FloatLiteral(n[0])}, {FloatLiteral(n[0])}, {FloatLiteral(n[0])}, {FloatLiteral(n[0])})";

                if (n is { Count: 2 })
                    return $"{thickness}({FloatLiteral(n[1])}, {FloatLiteral(n[0])}, {FloatLiteral(n[1])}, {FloatLiteral(n[0])})";

                if (n is { Count: 4 })
                    return $"{thickness}({FloatLiteral(n[3])}, {FloatLiteral(n[0])}, {FloatLiteral(n[1])}, {FloatLiteral(n[2])})";

                return Fail("expected one, two or four numbers (top right bottom left, as in SUIL)");
            }
        }

        return Fail("this type is not supported by SUIL yet (string, bool, numbers, enums, Vector2 and Thickness are)");
    }

    private static string EnumMember(INamedTypeSymbol enumType, List<ISuilValue> values, Func<string, string> fail)
    {
        if (values.Count != 1 || values[0] is not SuilIdent ident)
            return fail("expected the name of an enum member");

        var wanted = Normalize(ident.Value);
        var members = enumType.GetMembers().OfType<IFieldSymbol>().Where(f => f.HasConstantValue).ToList();
        var member = members.FirstOrDefault(f => Normalize(f.Name) == wanted);

        if (member == null)
        {
            return fail($"'{ident.Value}' is not a member of {enumType.ToDisplayString()}: " +
                        string.Join(", ", members.Select(m => m.Name)));
        }

        return $"{enumType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}.{member.Name}";

        static string Normalize(string name) => name.Replace("-", string.Empty).Replace("_", string.Empty).ToLowerInvariant();
    }

    private static string LocalizationCall(SuilLocalization loc, SuilProperty property)
    {
        var key = Literal(string.Concat(loc.Key.Select(p => p.Text)));
        const string call = "global::Robust.Shared.Localization.Loc.GetString";

        if (loc.Arguments.Count == 0)
            return $"{call}({key})";

        var arguments = loc.Arguments.Select(a =>
        {
            if (a.Value.Count != 1)
            {
                throw new SuilGenerationException(SuilDiagnostics.BadValue, property.Position,
                    $"The localization argument '{a.Name}' must be one value");
            }

            string value;
            switch (a.Value[0])
            {
                case SuilString str:
                    value = Literal(string.Concat(str.Parts.Select(p => p.Text)));
                    break;
                case SuilIdent ident:
                    value = Literal(ident.Value);
                    break;
                case var other when TryNumber(other, out var number, out var integer):
                    value = integer ? $"{NumberLiteral(number, true)}" : $"{NumberLiteral(number, false)}";
                    break;
                default:
                    throw new SuilGenerationException(SuilDiagnostics.BadValue, property.Position,
                        $"The localization argument '{a.Name}' must be a string, an ident or a number");
            }

            return $"({Literal(a.Name)}, (object) {value})";
        });

        return $"{call}({key}, {string.Join(", ", arguments)})";
    }

    // ---- Literals ----

    private static bool TryNumber(ISuilValue value, out double number, out bool isInteger)
    {
        switch (value)
        {
            case SuilNumber n:
                number = n.Value;
                isInteger = n.IsInteger;
                return true;
            case SuilDimension { Unit: "px" } d:
                number = d.Value;
                isInteger = d.IsInteger;
                return true;
            default:
                number = 0;
                isInteger = false;
                return false;
        }
    }

    private static List<double>? Numbers(List<ISuilValue> values)
    {
        var result = new List<double>();

        foreach (var value in values)
        {
            if (!TryNumber(value, out var number, out _))
                return null;

            result.Add(number);
        }

        return result;
    }

    private static bool IsNumeric(SpecialType type, out bool isIntegral)
    {
        isIntegral = type is SpecialType.System_SByte or SpecialType.System_Byte or SpecialType.System_Int16
            or SpecialType.System_UInt16 or SpecialType.System_Int32 or SpecialType.System_UInt32
            or SpecialType.System_Int64 or SpecialType.System_UInt64;

        return isIntegral || type is SpecialType.System_Single or SpecialType.System_Double;
    }

    private static string CSharpKeyword(SpecialType type) => type switch
    {
        SpecialType.System_SByte => "sbyte",
        SpecialType.System_Byte => "byte",
        SpecialType.System_Int16 => "short",
        SpecialType.System_UInt16 => "ushort",
        SpecialType.System_Int32 => "int",
        SpecialType.System_UInt32 => "uint",
        SpecialType.System_Int64 => "long",
        SpecialType.System_UInt64 => "ulong",
        SpecialType.System_Single => "float",
        _ => "double",
    };

    private static string NumberLiteral(double number, bool isInteger)
        => isInteger
            ? ((long) number).ToString(CultureInfo.InvariantCulture)
            : number.ToString("R", CultureInfo.InvariantCulture);

    private static string FloatLiteral(double number)
        => number.ToString("R", CultureInfo.InvariantCulture) + "f";

    private static string Literal(string text) => SymbolDisplay.FormatLiteral(text, true);

    // ---- The members of ISuilView ----

    /// <summary>
    /// The implementation of <c>ISuilView</c>: the path of the file and the fields by the ids of the elements, so the hot
    /// reload binds a new control to its field without reflection.
    /// </summary>
    private void WriteViewMembers(StringBuilder sb, string viewPath)
    {
        const string view = "global::Content.Suil.ISuilView";
        var path = viewPath.EndsWith(".suil", StringComparison.Ordinal)
            ? viewPath.Substring(0, viewPath.Length - ".suil".Length)
            : viewPath;
        var ids = string.Join(", ", _fields.Select(f => Literal(f.Id)));

        sb.AppendLine($"        string {view}.SuilPath => {Literal(path)};");
        sb.AppendLine();
        sb.AppendLine($"        private static readonly string[] __suilFieldIds = new string[] {{ {ids} }};");
        sb.AppendLine($"        global::System.Collections.Generic.IReadOnlyList<string> {view}.SuilFieldIds => __suilFieldIds;");
        sb.AppendLine();
        sb.AppendLine($"        global::System.Type? {view}.SuilFieldType(string id)");
        sb.AppendLine("        {");
        sb.AppendLine("            switch (id)");
        sb.AppendLine("            {");

        foreach (var (id, _, type, _) in _fields)
        {
            sb.AppendLine($"                case {Literal(id)}: return typeof({type});");
        }

        sb.AppendLine("                default: return null;");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine($"        object? {view}.SuilGetField(string id)");
        sb.AppendLine("        {");
        sb.AppendLine("            switch (id)");
        sb.AppendLine("            {");

        foreach (var (id, name, _, _) in _fields)
        {
            sb.AppendLine($"                case {Literal(id)}: return this.{name};");
        }

        sb.AppendLine("                default: return null;");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine($"        bool {view}.SuilSetField(string id, object? value)");
        sb.AppendLine("        {");
        sb.AppendLine("            switch (id)");
        sb.AppendLine("            {");

        var index = 0;

        foreach (var (id, name, type, _) in _fields)
        {
            sb.AppendLine($"                case {Literal(id)}:");
            sb.AppendLine($"                    if (value is {type} __v{index})");
            sb.AppendLine("                    {");
            sb.AppendLine($"                        this.{name} = __v{index};");
            sb.AppendLine("                        return true;");
            sb.AppendLine("                    }");
            sb.AppendLine();
            sb.AppendLine("                    return false;");
            index++;
        }

        sb.AppendLine("                default: return false;");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
    }

    // ---- The file ----

    private string Wrap(string viewPath)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated>");
        sb.AppendLine($"// SUIL view of {viewPath}. The file is regenerated on every build, don't edit it.");
        sb.AppendLine("// </auto-generated>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        var containers = new List<INamedTypeSymbol>();
        for (var t = viewClass; t != null; t = t.ContainingType)
        {
            containers.Insert(0, t);
        }

        var ns = viewClass.ContainingNamespace;
        var hasNamespace = ns is { IsGlobalNamespace: false };

        if (hasNamespace)
        {
            sb.AppendLine($"namespace {ns.ToDisplayString()}");
            sb.AppendLine("{");
        }

        foreach (var container in containers)
        {
            // the innermost part is the view: it is the one that implements the interface of the hot reload
            var declaration = ReferenceEquals(container, viewClass)
                ? $"    partial class {container.Name} : global::Content.Suil.ISuilView"
                : $"    partial class {container.Name}";

            sb.AppendLine(declaration);
            sb.AppendLine("    {");
        }

        foreach (var (_, name, type, access) in _fields)
        {
            sb.AppendLine($"        {access} {type} {name} = default!;");
        }

        if (_fields.Count > 0)
            sb.AppendLine();

        sb.AppendLine("        /// <summary>Builds the tree of the view. Call it from the constructor.</summary>");
        sb.AppendLine("        private void InitializeComponent()");
        sb.AppendLine("        {");

        if (_handlers.Count > 0)
            sb.AppendLine("            var __unsub = new global::System.Collections.Generic.List<global::System.Action>();");

        sb.Append(_body);
        sb.AppendLine(_handlers.Count > 0
            ? "            global::Content.Suil.SuilViewRegistry.Register(this, __unsub);"
            : "            global::Content.Suil.SuilViewRegistry.Register(this);");
        sb.AppendLine("        }");
        sb.AppendLine();
        WriteViewMembers(sb, viewPath);
        WriteMethodMembers(sb);

        for (var i = 0; i < containers.Count; i++)
        {
            sb.AppendLine("    }");
        }

        if (hasNamespace)
            sb.AppendLine("}");

        return sb.ToString();
    }

}
