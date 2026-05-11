using JetBrains.Annotations;
using Robust.Shared.Serialization;

namespace Content.Shared._RF.NPC.GOAP;

/// <summary>
/// An event raised to get the result of a condition check.
/// </summary>
/// <typeparam name="T">GOAP condition type.</typeparam>
/// <param name="Condition">GOAP condition.</param>
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
/// <param name="Success">Was the action startup successful?</param>
[PublicAPI, ByRefEvent]
public record struct GoapActionStartup<T>(T Action, bool Success) where T : GoapAction;

/// <summary>
/// Event raised to shutdown GOAP action.
/// </summary>
/// <typeparam name="T">GOAP action type.</typeparam>
/// <param name="Action">GOAP action.</param>
[PublicAPI, ByRefEvent]
public record struct GoapActionShutdown<T>(T Action) where T : GoapAction;

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

// Net messages

[Serializable, NetSerializable]
public sealed class GoapDebugInfoRequest(NetEntity target) : EntityEventArgs
{
    public NetEntity Target = target;
}

[Serializable, NetSerializable]
public sealed class GoapDebugInfoMessage(
    NetEntity target,
    GoapPlanDebugInfo? plan,
    GoapStaticGraphDebug graphDebug,
    GoapBreakpoint? breakpoint) : EntityEventArgs
{
    public NetEntity Target = target;
    public GoapPlanDebugInfo? Plan = plan;
    public GoapStaticGraphDebug GraphDebug = graphDebug;
    public readonly GoapBreakpoint? Breakpoint = breakpoint;
}

[Serializable, NetSerializable]
public sealed class GoapBreakpointMessage(GoapBreakpoint point) : EntityEventArgs
{
    public readonly GoapBreakpoint Point = point;
}

[Serializable, NetSerializable]
public sealed class GoapBreakpointRemoveMessage(GoapBreakpoint point) : EntityEventArgs
{
    public readonly GoapBreakpoint Point = point;
}
