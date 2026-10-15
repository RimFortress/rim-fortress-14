using Microsoft.CodeAnalysis;

namespace Content.Suil.Generator;

/// <summary>
/// One registration of a tag: a [SuilTag] on a class, a [SuilTag] on the assembly, or the Name of [SuilView].
/// <paramref name="Problem"/> is set when the attribute itself is malformed.
/// </summary>
internal sealed record SuilTagRegistration(string Name, INamedTypeSymbol? Type, Location Location, string? Problem = null);

/// <summary>
/// The tags of SUIL and the control types they stand for.
/// <list type="bullet">
/// <item>
/// By default the tag of a class is its name in kebab-case: <c>SpriteView</c> is <c>sprite-view</c>,
/// <c>RfButton</c> is <c>rf-button</c>. Nothing is needed for this, so every engine and game control works as it is.
/// </item>
/// <item>
/// A custom name is given with <c>[SuilTag]</c> or with the <c>Name</c> of <c>[SuilView]</c>. A class that has one is
/// known only by it, and an explicit name wins over the default name of another class.
/// </item>
/// </list>
/// The tag <c>_</c> is the type of an element written as <c>_</c>, it exists only if it is registered.
/// </summary>
internal sealed class SuilTagRegistry
{
    public const string UntypedTag = "_";

    private readonly Dictionary<string, List<INamedTypeSymbol>> _byTag = new();
    private readonly HashSet<INamedTypeSymbol> _explicitTypes = new(SymbolEqualityComparer.Default);

    private Compilation _compilation = null!;
    private INamedTypeSymbol _control = null!;
    private Dictionary<string, List<INamedTypeSymbol>>? _defaults;

    /// <summary>
    /// Registers the tags, reporting the malformed ones: they are skipped, the rest still work.
    /// </summary>
    public static SuilTagRegistry Build(
        IEnumerable<SuilTagRegistration> registrations,
        INamedTypeSymbol control,
        Compilation compilation,
        Action<Diagnostic> report)
    {
        var registry = new SuilTagRegistry { _compilation = compilation, _control = control };

        foreach (var registration in registrations)
        {
            var problem = registration.Problem ?? Validate(registration, control, compilation);

            if (problem != null)
            {
                report(Diagnostic.Create(SuilDiagnostics.InvalidTag, registration.Location, problem));
                continue;
            }

            if (!registry._byTag.TryGetValue(registration.Name, out var types))
                registry._byTag[registration.Name] = types = new List<INamedTypeSymbol>();

            if (!types.Contains(registration.Type!, SymbolEqualityComparer.Default))
                types.Add(registration.Type!);

            registry._explicitTypes.Add(registration.Type!);
        }

        return registry;
    }

    private static string? Validate(SuilTagRegistration registration, INamedTypeSymbol control, Compilation compilation)
    {
        // (the checks of the type are shared with the default names, see IsUsableControl)
        if (!IsValidTagName(registration.Name))
        {
            return $"'{registration.Name}' is not a valid tag: it must be '_' or start with a letter and have only " +
                   "letters, digits, '-' and '_'";
        }

        var type = registration.Type;

        if (type == null)
            return "the type is missing";

        if (type.TypeKind != TypeKind.Class || type.IsAbstract || type.IsStatic || type.TypeParameters.Length > 0)
            return $"'{type.ToDisplayString()}' must be a non-abstract non-generic class";

        if (!Derives(type, control))
            return $"'{type.ToDisplayString()}' is not a UI control (it doesn't derive from {control.ToDisplayString()})";

        if (!compilation.IsSymbolAccessibleWithin(type, compilation.Assembly))
            return $"'{type.ToDisplayString()}' is not accessible from this project";

        return null;
    }

    private static bool IsUsableControl(INamedTypeSymbol type, INamedTypeSymbol control, Compilation compilation)
    {
        return type.TypeKind == TypeKind.Class
               && !type.IsAbstract
               && !type.IsStatic
               && type.TypeParameters.Length == 0
               && compilation.IsSymbolAccessibleWithin(type, compilation.Assembly)
               && Derives(type, control);
    }

    public static bool IsValidTagName(string name)
    {
        if (name == UntypedTag)
            return true;

        if (name.Length == 0 || !char.IsLetter(name[0]))
            return false;

        return name.All(c => char.IsLetterOrDigit(c) || c is '-' or '_');
    }

    public static bool Derives(INamedTypeSymbol type, INamedTypeSymbol baseType)
    {
        for (INamedTypeSymbol? current = type; current != null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Every tag and the type it stands for: the registered names, and the default names of the types that have no
    /// registered name. <paramref name="owner"/>, if given, limits the types to the ones of the assembly.
    /// </summary>
    public IEnumerable<(string Tag, INamedTypeSymbol Type)> Entries(IAssemblySymbol? owner)
    {
        foreach (var pair in _byTag)
        {
            foreach (var type in pair.Value)
            {
                if (owner == null || SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, owner))
                    yield return (pair.Key, type);
            }
        }

        _defaults ??= BuildDefaults();

        foreach (var pair in _defaults)
        {
            // a registered name wins over the default name of another class, the same way as TryResolve does
            if (_byTag.ContainsKey(pair.Key))
                continue;

            foreach (var type in pair.Value)
            {
                if (_explicitTypes.Contains(type))
                    continue;

                if (owner == null || SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, owner))
                    yield return (pair.Key, type);
            }
        }
    }

    /// <summary>
    /// Finds the type of a tag: a registered name first, then the default name (the class name in kebab-case).
    /// Several different types under one tag are not resolved: <paramref name="ambiguous"/> has them.
    /// </summary>
    public bool TryResolve(string tag, out INamedTypeSymbol? type, out IReadOnlyList<INamedTypeSymbol> ambiguous)
    {
        type = null;
        ambiguous = Array.Empty<INamedTypeSymbol>();

        if (!_byTag.TryGetValue(tag, out var types))
        {
            // the default names are looked for only when the tag is not registered: the index is built on demand
            _defaults ??= BuildDefaults();

            if (!_defaults.TryGetValue(tag, out var defaults))
                return false;

            types = defaults.Where(t => !_explicitTypes.Contains(t)).ToList();

            if (types.Count == 0)
                return false;
        }

        if (types.Count == 1)
        {
            type = types[0];
            return true;
        }

        ambiguous = types;
        return false;
    }

    /// <summary>
    /// The usable controls of the project and of the <c>Content</c> and <c>Robust</c> namespaces by their default tag.
    /// </summary>
    private Dictionary<string, List<INamedTypeSymbol>> BuildDefaults()
    {
        var result = new Dictionary<string, List<INamedTypeSymbol>>();

        void Visit(INamespaceSymbol ns)
        {
            foreach (var member in ns.GetMembers())
            {
                switch (member)
                {
                    case INamespaceSymbol child:
                        Visit(child);
                        break;
                    case INamedTypeSymbol type when IsUsableControl(type, _control, _compilation):
                        var tag = SuilNaming.ToKebab(type.Name);

                        if (!result.TryGetValue(tag, out var list))
                            result[tag] = list = new List<INamedTypeSymbol>();

                        if (!list.Contains(type, SymbolEqualityComparer.Default))
                            list.Add(type);

                        break;
                }
            }
        }

        // everything of this project, and the engine and the game by their root namespaces
        Visit(_compilation.Assembly.GlobalNamespace);

        foreach (var root in _compilation.GlobalNamespace.GetNamespaceMembers())
        {
            if (root.Name is "Content" or "Robust")
                Visit(root);
        }

        return result;
    }
}

internal static class SuilNaming
{
    /// <summary>
    /// <c>SpriteView</c> to <c>sprite-view</c>. A run of capitals is one word until the last of them starts the next:
    /// <c>UIScale</c> is <c>ui-scale</c>, <c>RfHScrollBar</c> is <c>rf-h-scroll-bar</c>. Digits stay with the word before.
    /// </summary>
    public static string ToKebab(string name)
    {
        var result = new System.Text.StringBuilder(name.Length + 4);

        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];

            if (c == '_')
            {
                result.Append('-');
                continue;
            }

            if (char.IsUpper(c))
            {
                var afterLowerOrDigit = i > 0 && (char.IsLower(name[i - 1]) || char.IsDigit(name[i - 1]));
                var endsAcronym = i > 0 && char.IsUpper(name[i - 1]) && i + 1 < name.Length && char.IsLower(name[i + 1]);

                if (afterLowerOrDigit || endsAcronym)
                    result.Append('-');

                result.Append(char.ToLowerInvariant(c));
                continue;
            }

            result.Append(c);
        }

        return result.ToString();
    }

    /// <summary>
    /// <c>delete-button</c> to <c>DeleteButton</c>.
    /// </summary>
    public static string ToPascal(string name)
    {
        var result = new System.Text.StringBuilder(name.Length);
        var upperNext = true;

        foreach (var c in name)
        {
            if (c is '-' or '_')
            {
                upperNext = true;
                continue;
            }

            result.Append(upperNext ? char.ToUpperInvariant(c) : c);
            upperNext = false;
        }

        return result.ToString();
    }
}
