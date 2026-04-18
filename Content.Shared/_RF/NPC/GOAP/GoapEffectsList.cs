using System.Collections;

/// <summary>
/// List of effects that can be applied to GoapState.
/// </summary>
[Serializable]
public readonly record struct GoapEffectsList(Dictionary<string, object> Effects) : IEnumerable<KeyValuePair<string, object>>
{
    public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => Effects.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => Effects.GetEnumerator();
}
