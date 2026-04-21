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
/// <param name="Dump">Debug output of GOAP condtition check.</param>
[PublicAPI, ByRefEvent]
public record struct GoapConditionCheck<T>(
    T Condition,
    GoapState State,
    bool Result,
    GoapDebugDump Dump) where T : BaseGoapCondition<T>;

/// <summary>
/// Event raised to update a GOAP action.
/// </summary>
/// <typeparam name="T">GOAP action type.</typeparam>
/// <param name="Action">GOAP action.</param>
/// <param name="Result">Action execution result.</param>
/// <param name="Dump">Debug output of GOAP action update status.</param>
[PublicAPI, ByRefEvent]
public record struct GoapActionUpdate<T>(
    T Action,
    GoapActionResult Result,
    GoapDebugDump Dump) where T : GoapAction;

/// <summary>
/// Event raised to get GOAP action cost.
/// </summary>
/// <typeparam name="T">GOAP action type.</typeparam>
/// <param name="Action">GOAP action.</param>
/// <param name="State">
/// The state against which the check should be performed.
/// It may differ from the agent's actual state.
/// </param>
/// <param name="Cost">Action execution cost.</param>
[PublicAPI, ByRefEvent]
public record struct GoapActionCost<T>(T Action, GoapState State, float Cost) where T : GoapAction;

/// <summary>
/// Event raised to startup GOAP action.
/// </summary>
/// <typeparam name="T">GOAP action type.</typeparam>
/// <param name="Action">GOAP action.</param>
/// <param name="Dump">Debug output about GOAP action startup.</param>
[PublicAPI, ByRefEvent]
public record struct GoapActionStartup<T>(T Action, GoapDebugDump Dump) where T : GoapAction;

/// <summary>
/// Event raised to shutdown GOAP action.
/// </summary>
/// <typeparam name="T">GOAP action type.</typeparam>
/// <param name="Action">GOAP action.</param>
/// <param name="Dump">Debug output about GOAP action shutdown.</param>
[PublicAPI, ByRefEvent]
public record struct GoapActionShutdown<T>(T Action, GoapDebugDump Dump) where T : GoapAction;

/// <summary>
/// Event raised when GOAP planning fails.
/// </summary>
/// <param name="GoalState">State that was the goal for the failed plan.</param>
[PublicAPI]
public readonly record struct GoapPlaningFailed(GoapState GoalState);

/// <summary>
/// Event raised when GOAP plan finished.
/// </summary>
/// <param name="Reason">The reason why the plan was finished.</param>
/// <param name="GoalState">State that was the goal for the finished plan.</param>
[PublicAPI]
public readonly record struct GoapPlanFinished(GoapPlanFinishReason Reason, GoapState GoalState);