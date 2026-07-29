using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.UtilityAi.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._RF.NPC.UtilityAi;

[Serializable, NetSerializable]
public readonly record struct UtilityAiDebugInfo(
    ProtoId<UtilityAiGoalPrototype>? CurrentGoal,
    UtilityAiStaticGraph Graph);

[Serializable, NetSerializable]
public readonly record struct UtilityAiStaticGraph(
    List<UtilityAiGoalDebugInfo> Nodes,
    List<UtilityAiStaticGraphEdge> Edges,
    Dictionary<int, List<UtilityAiStaticGraphEdge>> OutgoingByNodeId,
    Dictionary<int, List<UtilityAiStaticGraphEdge>> IncomingByNodeId)
    : IStaticGraph<UtilityAiGoalDebugInfo, UtilityAiStaticGraphEdge>;

[Serializable, NetSerializable]
public readonly record struct UtilityAiGoalDebugInfo(
    int Id,
    ProtoId<UtilityAiGoalPrototype> ProtoId,
    UtilityAiConditionDebugDump[] Preconditions,
    GoapStateDebugDump GoalState,
    UtilityAiCurveDebugDump[] Curves,
    TimeSpan Cooldown,
    float Penalty,
    float Modified,
    float Result,
    bool AgentGoal,
    bool FallbackGoal,
    bool ExecutableGoal,
    bool InActiveBranch) : IStaticGraphNode;

[Serializable, NetSerializable]
public readonly record struct  UtilityAiStaticGraphEdge(
    int FromNodeId,
    int ToNodeId) : IStaticGraphEdge;

[Serializable, NetSerializable]
public readonly record struct UtilityAiCurveDebugDump(
    ObjectDebugReflection Reflection,
    float Input,
    float Output);

[Serializable, NetSerializable]
public readonly record struct UtilityAiConditionDebugDump(
    ObjectDebugReflection Reflection,
    GoapDebugDump Dump,
    bool Result);
