using Robust.Shared.Serialization;

namespace Content.Shared._RF.NPC.GOAP;

/// <summary>
/// Representation of a single GOAP task used by the planner.
/// </summary>
/// <param name="Actions">Sequence of actions that make up this task.</param>
/// <param name="Preconditions">Conditions that must hold before this task can be executed.</param>
/// <param name="Effects">State changes that occur after successful execution.</param>
public readonly record struct ExecutableGoapTask(
    IReadOnlyList<GoapAction> Actions,
    IReadOnlyList<GoapCondition> Preconditions,
    GoapState Effects);

/// <summary>
/// The current plan for a GOAP NPC.
/// </summary>
/// <param name="Actions">List of actions to perform during the current plan.</param>
/// <param name="Index">Current action index.</param>
/// <param name="ActionsDebug">Debug information for each action in the plan.</param>
public record struct GoapPlan(List<GoapAction> Actions, int Index, List<GoapActionDebugInfo>? ActionsDebug = null)
{
    public readonly GoapAction CurrentAction => Actions[Index];
}

/// <summary>
/// Possible reasons for the cancellation of the GOAP plan.
/// </summary>
[Serializable, NetSerializable]
public enum GoapPlanFinishReason : byte
{
    /// <summary>
    /// The plan has been fully completed.
    /// </summary>
    Finished,

    /// <summary>
    /// The plan failed to be completed due to the failure of a particular action.
    /// </summary>
    Failed,

    /// <summary>
    /// The execution of the plan was interrupted for reasons beyond the agent's control.
    /// For example, the plan was replaced by another one.
    /// </summary>
    Interrupted,
}