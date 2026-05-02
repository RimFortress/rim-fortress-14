using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.UtilityAi.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._RF.NPC.UtilityAi;

[Serializable, NetSerializable]
public readonly record struct UtilityAiDebugInfo(
    ProtoId<UtilityAiGoalPrototype>? CurrentGoal,
    UtilityAiGoalDebugInfo[] Goals);

[Serializable, NetSerializable]
public readonly record struct UtilityAiGoalDebugInfo(
    ProtoId<UtilityAiGoalPrototype> Id,
    UtilityAiConditionDebugDump[] Preconditions,
    GoapStateDebugDump GoalState,
    UtilityAiCurveDebugDump[] Curves,
    TimeSpan Cooldown,
    float Penalty,
    float Modified,
    float Result,
    bool AgentGoal);

[Serializable, NetSerializable]
public readonly record struct UtilityAiCurveDebugDump(
    Dictionary<string, (string Type, string Value)> Reflection,
    string Type,
    float Input,
    float Output);

[Serializable, NetSerializable]
public readonly record struct UtilityAiConditionDebugDump(
    Dictionary<string, (string Type, string Value)> Reflection,
    string Type,
    GoapDebugDump Dump,
    bool Result);
