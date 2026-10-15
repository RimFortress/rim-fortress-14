namespace Content.Suil;

/// <summary>
/// The control types by their tags: <c>css-button</c> is <c>CssButton</c>, or whatever a custom name was given to.
/// The generator writes it for every assembly, the runtime has no reflection to find the names by.
/// </summary>
public sealed class SuilTagTable
{
    private readonly Dictionary<string, List<Type>> _byTag = new(StringComparer.Ordinal);

    public void Add(string tag, Type type)
    {
        if (!_byTag.TryGetValue(tag, out var types))
            _byTag[tag] = types = new List<Type>();

        if (!types.Contains(type))
            types.Add(type);
    }

    public void Clear() => _byTag.Clear();

    public IEnumerable<string> Tags => _byTag.Keys;

    /// <summary>
    /// Finds the type of a tag. A tag that stands for several types is not resolved: <paramref name="candidates"/>
    /// has them.
    /// </summary>
    public bool TryGet(string tag, out Type? type, out IReadOnlyList<Type> candidates)
    {
        type = null;
        candidates = Array.Empty<Type>();

        if (!_byTag.TryGetValue(tag, out var types))
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
