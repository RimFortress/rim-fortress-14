using System.Collections;
using JetBrains.Annotations;

namespace Content.Shared._RF.NPC.GOAP;

/// <summary>
/// List of effects that can be applied to GoapState.
/// </summary>
[Serializable]
public readonly record struct GoapEffectsList(Dictionary<string, object> Effects) : IEnumerable<KeyValuePair<string, object>>
{
    public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => Effects.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => Effects.GetEnumerator();

    [PublicAPI]
    public GoapStateDebugDump ToDump()
    {
        var state = new Dictionary<string, (string, string)>();

        foreach (var (key, value) in Effects)
        {
            state[key] = (value.GetType().ToString(), value.ToString() ?? "null");
        }

        return new GoapStateDebugDump(state);
    }
}
