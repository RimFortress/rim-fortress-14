using Content.Shared._RF.NPC.Search.Prototypes;
using Content.Shared._RF.NPC.Search.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.NPC.Search.Components;

/// <summary>
/// A component that stores a cache of entity search results.
/// </summary>
[RegisterComponent]
[Access(typeof(NpcSearcherSystem))]
public sealed partial class NpcSearcherComponent : Component
{
    [DataField]
    public Dictionary<ProtoId<NpcSearcherQueryPrototype>, QueryResult> Queries = new();

    [Serializable]
    public readonly record struct QueryResult(TimeSpan ValidUntil, List<EntityUid> Result);
}
