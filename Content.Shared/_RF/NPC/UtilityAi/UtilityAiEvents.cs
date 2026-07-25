using Content.Shared._RF.NPC.UtilityAi.Prototypes;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._RF.NPC.UtilityAi;

/// <summary>
/// An event triggered to allow other systems to modify the Utility AI goal score.
/// </summary>
/// <param name="Goal">Utility AI goal prototype.</param>
/// <param name="Score">Goal score.</param>
[ByRefEvent]
public record struct UtilityAiGoalScoreModify(ProtoId<UtilityAiGoalPrototype> Goal, float Score);

/// <summary>
/// An event triggered when a GOAP agent receives a Utility AI goal.
/// </summary>
/// <param name="Goal">Utility AI goal prototype.</param>
[PublicAPI]
public record struct UtilityAiGoalGiven(ProtoId<UtilityAiGoalPrototype> Goal);

// Executable goals

/// <summary>
/// Invoked when a user who can control this entity is added.
/// </summary>
/// <param name="User">User entity.</param>
[PublicAPI]
public record struct NpcControllerAdded(EntityUid User);

// Net Messages

[Serializable, NetSerializable]
public sealed class UtilityAiDebugInfoRequest(NetEntity target) : EntityEventArgs
{
    public NetEntity Target = target;
}

[Serializable, NetSerializable]
public sealed class UtilityAiDebugInfoMessage(NetEntity target, UtilityAiDebugInfo info) : EntityEventArgs
{
    public NetEntity Target = target;
    public UtilityAiDebugInfo Info = info;
}
