namespace Content.Suil;

/// <summary>
/// The control types by their tags: <c>rf-button</c> is <c>RfButton</c>, or whatever a custom name was given to.
/// The generator writes it for every assembly, the runtime has no reflection to find the names by.
/// </summary>
public sealed class SuilTagTable
{
    private readonly Dictionary<string, List<Type>> _byTag = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<Type>> _explicit = new(StringComparer.Ordinal);

    public void Add(string tag, Type type)
    {
        if (!_byTag.TryGetValue(tag, out var types))
            _byTag[tag] = types = new List<Type>();

        if (!types.Contains(type))
            types.Add(type);
    }

    /// <summary>
    /// Adds a tag that was given to a class on purpose (<c>@Class as tag =&gt; ...</c>). It wins over the tags that
    /// controls have by their names: <c>option-button</c> is the one that was given, not the engine's
    /// <c>OptionButton</c>.
    /// </summary>
    public void AddExplicit(string tag, Type type)
    {
        if (!_explicit.TryGetValue(tag, out var types))
            _explicit[tag] = types = new List<Type>();

        if (!types.Contains(type))
            types.Add(type);
    }

    public void Clear()
    {
        _byTag.Clear();
        _explicit.Clear();
    }

    public IEnumerable<string> Tags
    {
        get
        {
            foreach (var tag in _byTag.Keys)
            {
                yield return tag;
            }

            foreach (var tag in _explicit.Keys)
            {
                if (!_byTag.ContainsKey(tag))
                    yield return tag;
            }
        }
    }

    /// <summary>
    /// Finds the type of a tag. A tag that stands for several types is not resolved: <paramref name="candidates"/>
    /// has them.
    /// </summary>
    public bool TryGet(string tag, out Type? type, out IReadOnlyList<Type> candidates)
    {
        type = null;
        candidates = Array.Empty<Type>();

        if (!_explicit.TryGetValue(tag, out var types) && !_byTag.TryGetValue(tag, out types))
            return false;

        if (types.Count == 1)
        {
            type = types[0];
            return true;
        }

        candidates = types;
        return false;
    }
}
