namespace Content.Suil;

/// <summary>
/// A call of a UI controller or of an entity system that a file makes, <c>@Controllers.Zoning.Pick()</c>: the parameters
/// of the method and the way to call it.
/// </summary>
/// <param name="Parameters">The types of the parameters of the method.</param>
/// <param name="Invoke">Calls the method with the arguments, which must be of the types of the parameters.</param>
public sealed record SuilCallEntry(Type[] Parameters, Action<object?[]> Invoke);

/// <summary>
/// The calls of controllers and systems that the files of an assembly make, found when it was built. The hot reload can't
/// find a method by its name (the sandbox has no reflection), so it can call only what some file called already.
/// </summary>
public sealed class SuilCallTable
{
    private readonly Dictionary<string, List<SuilCallEntry>> _byName = new(StringComparer.Ordinal);

    /// <param name="key"><c>Controllers.Zoning.Pick</c>: the kind of the class, its name and the method.</param>
    public void Add(string key, Type[] parameters, Action<object?[]> invoke)
    {
        if (!_byName.TryGetValue(key, out var entries))
            _byName[key] = entries = new List<SuilCallEntry>();

        entries.Add(new SuilCallEntry(parameters, invoke));
    }

    public void Clear() => _byName.Clear();

    /// <summary>
    /// The overloads of a call that take this many arguments.
    /// </summary>
    public IEnumerable<SuilCallEntry> Find(string key, int arity)
    {
        if (!_byName.TryGetValue(key, out var entries))
            yield break;

        foreach (var entry in entries)
        {
            if (entry.Parameters.Length == arity)
                yield return entry;
        }
    }

    public bool Has(string key) => _byName.ContainsKey(key);
}

/// <summary>
/// The generated calls of an assembly. Every assembly that the generator runs on and that has calls in its files has a
/// class that implements it; the style manager finds them all.
/// </summary>
public interface ISuilCallTables
{
    void RegisterCalls(SuilCallTable calls);
}
