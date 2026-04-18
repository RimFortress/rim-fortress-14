using JetBrains.Annotations;

namespace Content.Shared._RF.NPC.GOAP;

/// <summary>
/// An event raised to get the result of a condition check.
/// </summary>
/// <typeparam name="T">GOAP condtition type.</typeparam>
/// <param name="Condition">GOAP condtition.</param>
/// <param name="State">
/// The state against which the check should be performed.
/// It may differ from the agent's actual state.
/// </param>
/// <param name="Result">Check result.</param>
[PublicAPI, ByRefEvent]
public record struct GoapConditionCheck<T>(T Condition, GoapState State, bool Result) where T : BaseGoapCondition<T>;

/// <summary>
/// Event raised to update a GOAP action.
/// </summary>
/// <typeparam name="T">GOAP action type.</typeparam>
/// <param name="Action">GOAP action.</param>
/// <param name="Result">Action execution result.</param>
[PublicAPI, ByRefEvent]
public record struct GoapActionUpdate<T>(T Action, GoapActionResult Result) where T : GoapAction;
