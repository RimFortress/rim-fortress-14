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
