using Content.Shared._RF.NPC.GOAP.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._RF.NPC.GOAP;

/// <summary>
/// Representation of a single GOAP task used by the planner.
/// </summary>
/// <param name="Actions">Sequence of actions that make up this task.</param>
/// <param name="Preconditions">Conditions that must hold before this task can be executed.</param>
/// <param name="Services">
/// Services that provide additional effects during the planning phase,
/// which require additional calculations.
/// </param>
/// <param name="Effects">State changes that occur after successful execution.</param>
/// <param name="Compound">The compound from which this task was extracted.</param>
[Serializable]
public readonly record struct ExecutableGoapTask(
    IReadOnlyList<GoapAction> Actions,
    IReadOnlyList<GoapCondition> Preconditions,
    IReadOnlyList<GoapService> Services,
    GoapState Effects,
    ProtoId<GoapCompoundPrototype>? Compound);

/// <summary>
/// The current plan for a GOAP NPC.
/// </summary>
/// <param name="Actions">List of actions to perform during the current plan.</param>
/// <param name="Index">Current action index.</param>
[Serializable]
public record struct GoapPlan(List<GoapAction> Actions, int Index, Dictionary<int, GoapState> ServiceEffects)
{
    public readonly GoapAction CurrentAction => Actions[Index];
    public readonly GoapState CurrentEffect => ServiceEffects.GetValueOrDefault(Index, new());
    public GoapPlan MoveNext() => this with { Index = Index + 1 };
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
