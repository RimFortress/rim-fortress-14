using Content.Shared._RF.NPC.GOAP;
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

/// <summary>
/// An event raised after the current goal's plan has been finished, but before a new goal is set.
/// It allows other systems to intervene in the logic of selecting a new goal.
/// </summary>
/// <param name="Goal">Finished Utility AI goal prototype.</param>
/// <param name="Reason">The reason why the goal completion ended.</param>
/// <param name="Handled">
/// Has this event been handled by any system?
/// If true, Utility AI will not search for a new goal for the agent.
/// </param>
[PublicAPI, ByRefEvent]
public record struct BeforeUtilityAiGoalFinished(
    ProtoId<UtilityAiGoalPrototype> Goal,
    UtilityAiGoalFinishReason Reason,
    bool Handled);

/// <summary>
/// Invoked each time the Utility AI goal is completed.
/// </summary>
/// <remarks>
/// Basically, it's just a layer on top of <see cref="GoapPlanFinished"/>
/// and <see cref="GoapPlaningFailed"/> that allows you to avoid dealing with low-level AI.
/// </remarks>
/// <param name="Goal">Utility AI goal.</param>
/// <param name="Reason">The reason why the goal completion ended.</param>
[PublicAPI]
public record struct UtilityAiGoalFinished(
    ProtoId<UtilityAiGoalPrototype> Goal,
    UtilityAiGoalFinishReason Reason);

[Serializable, NetSerializable]
public enum UtilityAiGoalFinishReason : byte
{
    /// <summary>
    /// The agent successfully reached the goal.
    /// </summary>
    Finished,

    /// <summary>
    /// The agent was unable to plan a path to achieve the goal or failed to execute the plan.
    /// </summary>
    Failed,

    /// <summary>
    /// The execution of the goal was interrupted for reasons beyond the agent's control.
    /// For example, the goal was replaced by another one.
    /// </summary>
    Interrupted,
}

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
