using Content.Shared._RF.NPC.Search.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._RF.NPC.Search;

/// <summary>
/// Debug information about the NPC search execution.
/// </summary>
/// <param name="ProtoId">Search query prototype.</param>
/// <param name="Query">A search query whose results will be filtered and scored.</param>
/// <param name="Filters">Search query filters.</param>
/// <param name="Considerations">Search query considerations.</param>
/// <param name="Results">Search results.</param>
[Serializable, NetSerializable]
public readonly record struct NpcSearchDebugInfo(
    int Id,
    ProtoId<SearchQueryPrototype> ProtoId,
    (ObjectDebugReflection Reflection, HashSet<string> Result) Query,
    List<(ObjectDebugReflection Reflection, HashSet<string> Filtered)> Filters,
    List<(ObjectDebugReflection Reflection, Dictionary<string, float> Result)> Considerations,
    Dictionary<string, (List<float> Parts, float Result)> Results) : IStaticGraphNode;

// Dummy graph, just for UI
[Serializable, NetSerializable]
public readonly record struct NpcSearchGraph(
    List<NpcSearchDebugInfo> Nodes,
    List<NpcSearchGraphEdge> Edges,
    Dictionary<int, List<NpcSearchGraphEdge>> OutgoingByNodeId,
    Dictionary<int, List<NpcSearchGraphEdge>> IncomingByNodeId)
    : IStaticGraph<NpcSearchDebugInfo, NpcSearchGraphEdge>;

[Serializable, NetSerializable]
public readonly record struct  NpcSearchGraphEdge(
    int FromNodeId,
    int ToNodeId) : IStaticGraphEdge;

// Net Messages

[Serializable, NetSerializable]
public sealed class NpcSearchDebugInfoRequest(NetEntity target) : EntityEventArgs
{
    public NetEntity Target = target;
}

[Serializable, NetSerializable]
public sealed class NpcSearchDebugInfoMessage(NetEntity target, NpcSearchGraph info) : EntityEventArgs
{
    public NetEntity Target = target;
    public NpcSearchGraph Info = info;
}
