namespace Content.Suil;

/// <summary>
/// Reads and writes one property of a control by its SUIL name. The generator writes them for the public settable
/// members of the controls, the runtime has no reflection to do it.
/// </summary>
/// <param name="Name">The name of the property in SUIL: the member name in kebab-case, or the custom one.</param>
/// <param name="MemberName">The name of the member in C#.</param>
/// <param name="ClrType">The type of the value of the member.</param>
/// <param name="Setter">Sets the value of the property of a control (the control, the value).</param>
/// <param name="Getter">Gets the value of the property of a control; <c>null</c> for a property that can only be set.</param>
/// <param name="CustomParser">The type of the parser of the value, or <c>null</c> for the default parser of <paramref name="ClrType"/>.</param>
/// <param name="Inherited">Does a control without a value of its own take the one of its ancestor.</param>
public sealed record SuilPropertyAccessor(
    string Name,
    string MemberName,
    Type ClrType,
    Action<object, object?> Setter,
    Func<object, object?>? Getter,
    Type? CustomParser,
    bool Inherited);

/// <summary>
/// The generated tables of the controls of one assembly: their properties, their tags and their shorthands. Every assembly that the
/// generator runs on has a class that implements it; the style manager finds them all and lets each register its controls.
/// </summary>
public interface ISuilControlTables
{
    void RegisterProperties(SuilPropertyRegistry registry);

    void RegisterTags(SuilTagTable tags);

    void RegisterShorthands(SuilShorthandTable shorthands);
}

/// <summary>
/// The properties of the control types. A type has the properties it declares itself, the ones of its base types
/// are looked for up the chain, and a property of a derived type hides the one of the base type with the same name.
/// </summary>
public sealed class SuilPropertyRegistry
{
    private readonly Dictionary<Type, Func<IReadOnlyDictionary<string, SuilPropertyAccessor>>> _factories = new();
    private readonly Dictionary<Type, IReadOnlyDictionary<string, SuilPropertyAccessor>> _declared = new();
    private readonly Dictionary<Type, IReadOnlyDictionary<string, SuilPropertyAccessor>> _merged = new();

    /// <summary>
    /// Registers the properties declared by a type. The table is made when it is first needed.
    /// </summary>
    public void Register(Type declaringType, Func<IReadOnlyDictionary<string, SuilPropertyAccessor>> create)
    {
        _factories[declaringType] = create;
        _declared.Remove(declaringType);
        _merged.Clear();
    }

    /// <summary>
    /// Forgets everything.
    /// </summary>
    public void Clear()
    {
        _factories.Clear();
        _declared.Clear();
        _merged.Clear();
    }

    /// <summary>
    /// Whether the type declares properties by itself (the base types are not asked).
    /// </summary>
    public bool Declares(Type type) => _factories.ContainsKey(type);

    /// <summary>
    /// Finds the property <paramref name="name"/> of a control type, base types included.
    /// </summary>
    public bool TryGet(Type controlType, string name, out SuilPropertyAccessor? accessor)
        => GetAll(controlType).TryGetValue(name, out accessor);

    /// <summary>
    /// All properties of a control type, base types included (the keys are SUIL names).
    /// </summary>
    public IReadOnlyDictionary<string, SuilPropertyAccessor> GetAll(Type controlType)
    {
        if (_merged.TryGetValue(controlType, out var cached))
            return cached;

        var chain = new List<Type>();
        for (var type = controlType; type != null; type = type.BaseType)
        {
            chain.Add(type);
        }

        // Base types go first, so a property redeclared in a derived class wins.
        var merged = new Dictionary<string, SuilPropertyAccessor>(StringComparer.Ordinal);

        for (var i = chain.Count - 1; i >= 0; i--)
        {
            foreach (var pair in Declared(chain[i]))
            {
                merged[pair.Key] = pair.Value;
            }
        }

        _merged[controlType] = merged;
        return merged;
    }

    private IReadOnlyDictionary<string, SuilPropertyAccessor> Declared(Type type)
    {
        if (_declared.TryGetValue(type, out var table))
            return table;

        if (!_factories.TryGetValue(type, out var create))
            return _declared[type] = new Dictionary<string, SuilPropertyAccessor>();

        return _declared[type] = create();
    }
}
