using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared.Hands.EntitySystems;
using JetBrains.Annotations;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared._RF.NPC.GOAP.Systems;

public abstract class SharedGoapSystem : EntitySystem, IGoapConditionCheсker, IGoapActionPerformer, IGoapServiceChecker
{
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    protected readonly Dictionary<ICommonSession, List<GoapBreakpoint>> Breakpoints = new();
    protected readonly List<(ICommonSession Session, EntityUid Target, GoapBreakpoint? Breakpoint)> DebugSendQueue = new();

    #region Conditions

    public bool CheckCondition<T>(EntityUid target, GoapState state, T condition, out GoapDebugDump? dump) where T : BaseGoapCondition<T>
    {
        state.ReadOnly = true;
        condition.Dump = null;
        var ev = new GoapConditionCheck<T>(condition, state, true);
        RaiseLocalEvent(target, ref ev);
        state.ReadOnly = false;
        dump = condition.Dump;
        return ev.Result;
    }

    /// <summary>
    /// Checks whether the GOAP target entity satisfies the condition.
    /// </summary>
    /// <param name="uid">Target entity.</param>
    /// <param name="state">
    /// The state against which the check should be performed.
    /// It may differ from the agent's actual state.
    /// </param>
    /// <param name="condition">GOAP condtition/</param>
    /// <param name="dump">Debug dump.</param>
    /// <returns>True, if the check is passed; otherwise, false</returns>
    [PublicAPI]
    public bool CheckCondition(EntityUid uid, GoapState state, GoapCondition condition, out GoapDebugDump? dump)
        => condition.Check(uid, state, this, out dump);

    /// <inheritdoc cref="CheckCondition(EntityUid, GoapState, GoapCondition, out GoapDebugDump?)"/>
    [PublicAPI]
    public bool CheckCondition(EntityUid uid, GoapState state, GoapCondition condition)
        => condition.Check(uid, state, this, out _);

    /// <inheritdoc cref="CheckCondition(EntityUid, GoapState, GoapCondition, out GoapDebugDump?)"/>
    [PublicAPI]
    public bool CheckCondition(EntityUid uid, GoapState state, IEnumerable<GoapCondition> conditions)
    {
        foreach (var condition in conditions)
        {
            if (!CheckCondition(uid, state, condition))
                return false;
        }

        return true;
    }

    /// <inheritdoc cref="CheckCondition(EntityUid, GoapState, GoapCondition, out GoapDebugDump?)"/>
    [PublicAPI]
    public bool CheckCondition(Entity<GoapComponent?> ent, GoapCondition condition)
        => Resolve(ent, ref ent.Comp) && CheckCondition(ent, ent.Comp.State, condition);

    /// <inheritdoc cref="CheckCondition(EntityUid, GoapState, GoapCondition, out GoapDebugDump?)"/>
    [PublicAPI]
    public bool CheckCondition(Entity<GoapComponent?> ent, IEnumerable<GoapCondition> conditions)
        => Resolve(ent, ref ent.Comp) && CheckCondition(ent, ent.Comp.State, conditions);

    #endregion

    #region Services

    public Task<GoapState?> CheckService<T>(
        EntityUid target,
        GoapState state,
        T service,
        CancellationToken cancellation = default) where T : BaseGoapService<T>
    {
        service.Dump = null;
        state.ReadOnly = true;
        var ev = new GoapServiceCheck<T>(service, state, Task.FromResult<GoapState?>(null), cancellation);
        RaiseLocalEvent(target, ref ev);
        state.ReadOnly = false;
        return ev.Result;
    }

    /// <inheritdoc cref="CheckService{T}"/>
    public async Task<(GoapState? Result, GoapDebugDump? Dump)> CheckService(
        EntityUid target,
        GoapState state,
        GoapService service,
        CancellationToken cancellation = default)
    {
        var result = await service.Check(target, state, this, cancellation);
        return (result, service.Dump);
    }

    /// <inheritdoc cref="CheckService{T}"/>
    public async Task<GoapState?> CheckService(
        EntityUid target,
        GoapState state,
        IReadOnlyList<GoapService> services,
        CancellationToken cancellation = default)
    {
        var result = new GoapState();

        foreach (var service in services)
        {
            var effects = await service.Check(target, state, this, cancellation);

            if (effects == null)
                return null;

            foreach (var (key, value) in effects)
            {
                result.SetValue(key, value);
            }
        }

        return result;
    }

    #endregion

    #region Actions

    public GoapActionResult UpdateAction<T>(EntityUid target, T action, out GoapDebugDump? dump) where T : BaseGoapAction<T>
    {
        action.Dump = null;
        var ev = new GoapActionUpdate<T>(action, GoapActionResult.Continuing);
        RaiseLocalEvent(target, ref ev);
        dump = action.Dump;
        action.Dump = null;
        return ev.Result;
    }

    /// <summary>
    /// Updates the execution of a GOAP action.
    /// </summary>
    /// <param name="target">Target entity.</param>
    /// <param name="action">GOAP action.</param>
    /// <returns>Update result.</returns>
    protected GoapActionResult UpdateAction(EntityUid target, GoapAction action)
    {
#if TOOLS
        var result = action.Update(target, this, out var dump);
        var comp = Comp<GoapComponent>(target);
        DebugTools.Assert(
            comp is { Plan: not null, PlanDebug: not null },
            $"attempt to update action for an agent without a plan! Agent: {ToPrettyString(target)}, Action: {action.GetType().ToString()}");
        var plan = comp.Plan.Value;
        var planDebug = comp.PlanDebug.Value;
        DebugTools.Assert(plan.Actions.Count == planDebug.Actions.Count);

        // There's no need to spam every tick about action updates
        if (result != GoapActionResult.Continuing || dump?.Dump != null)
        {
            planDebug.Actions[plan.Index] = planDebug.Actions[plan.Index]
                .WithUpdate(new GoapActionUpdateDebugDump(
                    Timing.CurTick,
                    dump ?? new(null, comp.State.GetStateDump()),
                    result));
        }

        // Sending debug information to users when a breakpoint is hit
        var debugAction = planDebug.Actions[plan.Index];
        var netTarget = GetNetEntity(target);
        foreach (var (session, points) in Breakpoints)
        {
            for (var i = 0; i < points.Count; i++)
            {
                var point = points[i];

                if (point.Target != netTarget
                    || point.NodeId != debugAction.NodeIndex
                    || point.Index != debugAction.ActionIndex
                    || point.Kind != GoapBreakpointKind.ActionUpdate)
                    continue;

                switch (result)
                {
                    case GoapActionResult.Continuing
                        when point.Result != GoapBreakpointResultKind.Continuing:
                    case GoapActionResult.Failed
                        when point.Result != GoapBreakpointResultKind.Failed:
                    case GoapActionResult.Finished
                        when point.Result != GoapBreakpointResultKind.Finished:
                        continue;
                    default:
                        SendDebug(session, target, point);
                        RemoveBreakpoint(session, point);
                        i--;
                        break;
                }
            }
        }

        return result;
#else
        return action.Update(target, this, out _);
#endif
    }

    public float ActionCost<T>(EntityUid target, GoapState state, T action) where T : BaseGoapAction<T>
    {
        state.ReadOnly = true;
        var ev = new GoapActionCost<T>(action, state, 1f);
        RaiseLocalEvent(target, ref ev);
        state.ReadOnly = false;
        return ev.Cost;
    }

    /// <summary>
    /// Calculates the cost of executing a GOAP action.
    /// </summary>
    /// <param name="target">Target entity.</param>
    /// <param name="state">
    /// The state against which the check should be performed.
    /// It may differ from the agent's actual state.
    /// </param>
    /// <param name="action">GOAP action.</param>
    /// <returns>Action execution cost.</returns>
    [PublicAPI]
    public float ActionCost(EntityUid target, GoapState state, GoapAction action)
        => action.Cost(target, state, this);

    /// <inheritdoc cref="ActionCost(EntityUid, GoapState, GoapAction)"/>
    [PublicAPI]
    public float ActionCost(Entity<GoapComponent?> ent, GoapAction action)
        => Resolve(ent, ref ent.Comp) ? ActionCost(ent, ent.Comp.State, action) : 0f;

    public bool ActionStartup<T>(EntityUid target, T action, out GoapDebugDump? dump) where T : BaseGoapAction<T>
    {
        action.Dump = null;
        var ev = new GoapActionStartup<T>(action, true);
        RaiseLocalEvent(target, ref ev);
        dump = action.Dump;
        action.Dump = null;
        return ev.Success;
    }

    /// <summary>
    /// Starts the action.
    /// </summary>
    /// <param name="target">Target entity.</param>
    /// <param name="action">GOAP action.</param>
    /// <returns>True, if the action startup was successful.</returns>
    protected bool ActionStartup(EntityUid target, GoapAction action)
    {
        if (!TryComp(target, out GoapComponent? comp))
            return false;

        if (comp.Plan?.CurrentEffect.Count > 0)
            comp.State.OverwriteFrom(comp.Plan.Value.CurrentEffect);

#if TOOLS
        var success = action.Startup(target, this, out var dump);
        DebugTools.Assert(
            comp is { Plan: not null, PlanDebug: not null },
            $"attempt to startup action for an agent without a plan! Agent: {ToPrettyString(target)}, Action: {action.GetType().ToString()}");
        var plan = comp.Plan.Value;
        var planDebug = comp.PlanDebug.Value;
        DebugTools.Assert(plan.Actions.Count == planDebug.Actions.Count);
        planDebug.Actions[plan.Index] = planDebug.Actions[plan.Index].WithStartup(success, dump);

        // Sending debug information to users when a breakpoint is hit
        var debugAction = planDebug.Actions[plan.Index];
        var netTarget = GetNetEntity(target);
        foreach (var (session, points) in Breakpoints)
        {
            for (var i = 0; i < points.Count; i++)
            {
                var point = points[i];
                if (point.Target != netTarget
                    || point.NodeId != debugAction.NodeIndex
                    || point.Index != debugAction.ActionIndex
                    || point.Kind != GoapBreakpointKind.ActionStartup)
                    continue;

                switch (success)
                {
                    case true
                        when point.Result != GoapBreakpointResultKind.True:
                    case false
                        when point.Result != GoapBreakpointResultKind.False:
                        continue;
                    default:
                        SendDebug(session, target, point);
                        RemoveBreakpoint(session, point);
                        i--;
                        break;
                }
            }
        }

        return success;
#else
        return action.Startup(target, this, out _);
#endif
    }

    public void ActionShutdown<T>(EntityUid target, T action, out GoapDebugDump? dump) where T : BaseGoapAction<T>
    {
        action.Dump = null;
        var ev = new GoapActionShutdown<T>(action);
        RaiseLocalEvent(target, ref ev);
        dump = action.Dump;
        action.Dump = null;
    }

    /// <summary>
    /// Finishes the action.
    /// </summary>
    /// <param name="target">Target entity.</param>
    /// <param name="action">GOAP action.</param>
    protected void ActionShutdown(EntityUid target, GoapAction action)
    {
#if TOOLS
        action.Shutdown(target, this, out var dump);
        var comp = Comp<GoapComponent>(target);
        DebugTools.Assert(
            comp is { Plan: not null, PlanDebug: not null },
            $"attempt to shutdown action for an agent without a plan! Agent: {ToPrettyString(target)}, Action: {action.GetType().ToString()}");
        var plan = comp.Plan.Value;
        var planDebug = comp.PlanDebug.Value;
        DebugTools.Assert(plan.Actions.Count == planDebug.Actions.Count);
        planDebug.Actions[plan.Index] = planDebug.Actions[plan.Index].WithShutdown(dump);

        // Sending debug information to users when a breakpoint is hit
        var debugAction = planDebug.Actions[plan.Index];
        var netTarget = GetNetEntity(target);
        foreach (var (session, points) in Breakpoints)
        {
            for (var i = 0; i < points.Count; i++)
            {
                var point = points[i];

                if (point.Target != netTarget
                    || point.NodeId != debugAction.NodeIndex
                    || point.Index != debugAction.ActionIndex
                    || point.Kind != GoapBreakpointKind.ActionShutdown
                    || point.Result != GoapBreakpointResultKind.None)
                    continue;

                SendDebug(session, target, point);
                RemoveBreakpoint(session, point);
                i--;
            }
        }
#else
        action.Shutdown(target, this, out _);
#endif
    }

    #endregion

    /// <summary>
    /// Forces the entity to perform a re-planning.
    /// </summary>
    [PublicAPI]
    public void Replan(Entity<GoapComponent?> ent)
    {
        if (Resolve(ent, ref ent.Comp))
            ent.Comp.NextPlanning = Timing.CurTime;
    }

    /// <summary>
    /// Shutdowns the current NPC plan.
    /// </summary>
    [PublicAPI]
    public void PlanShutdown(Entity<GoapComponent> ent, GoapPlanFinishReason reason, bool shutdownAction = true)
    {
        DebugTools.Assert(ent.Comp.Plan != null);

        if (shutdownAction)
            ActionShutdown(ent, ent.Comp.Plan.Value.CurrentAction);

        ent.Comp.Plan = null;
        RaiseLocalEvent(ent, new GoapPlanFinished(reason, ent.Comp.GoalState));
    }

    /// <summary>
    /// Sets the goal state for the GOAP agent, which will be used during the next planning.
    /// </summary>
    /// <param name="ent">GOAP agent entity.</param>
    /// <param name="goalState">Goal state.</param>
    [PublicAPI]
    public void SetGoal(Entity<GoapComponent?> ent, GoapState goalState)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        ent.Comp.GoalState = goalState;
        Replan(ent);
    }

    /// <inheritdoc/>
    [PublicAPI, Pure]
    public bool TryGetValue<T>(
        GoapState state,
        string key,
        [NotNullWhen(true)] out T? value) where T : notnull
    {
        if (state.TryGetValue(key, out value))
            return true;

        if (!TryGetStateDefaults(state, key, out var @default))
            return false;

        value = (T)@default;
        return true;
    }

    /// <inheritdoc/>
    [PublicAPI, Pure]
    public bool TryGetValue<T>(
        GoapState state,
        StateKey<T> key,
        [NotNullWhen(true)] out T? value) where T : notnull
        => TryGetValue(state, (string)key, out value);

    /// <inheritdoc/>
    [PublicAPI, Pure]
    public T GetValue<T>(GoapState state, string key) where T : notnull
    {
        if (TryGetStateDefaults(state, key, out var value))
            return (T)value;

        return state.GetValue<T>(key);
    }

    /// <inheritdoc/>
    [PublicAPI, Pure]
    public T GetValue<T>(GoapState state, StateKey<T> key) where T : notnull
        => GetValue<T>(state, (string)key);

    private bool TryGetStateDefaults(GoapState state, string key, [NotNullWhen(true)] out object? value)
    {
        value = null;
        var owner = state.GetValue(GoapState.Owner);

        if (key == GoapState.OwnerCoordinates)
        {
            value = Transform(owner).Coordinates;
            return true;
        }

        if (key == GoapState.ActiveHand)
        {
            if (_hands.GetActiveHand(owner) is not { } hand)
                return false;

            value = hand;
            return true;
        }

        if (key == GoapState.InContainer)
        {
            value = _container.IsEntityInContainer(owner);
            return true;
        }

        if (key == GoapState.ActiveHand)
        {
            value = _hands.ActiveHandIsEmpty(owner);
            return true;
        }

        if (key == GoapState.ActiveHandEntity)
        {
            if (!_hands.TryGetActiveItem(owner, out var uid))
                return false;

            value = uid;
            return true;
        }

        return false;
    }

    #region Debug

    protected virtual void SendDebug(
        ICommonSession session,
        EntityUid target,
        GoapBreakpoint? breakpoint = null)
    {
        // Noop on client
    }

    /// <summary>
    /// Adds the player to the list of those who will receive debug information about the NPC's next plan.
    /// </summary>
    protected virtual void QueueDebugSend(
        ICommonSession session,
        EntityUid target,
        GoapBreakpoint? breakpoint = null)
    {
        // Noop on client
    }

    /// <summary>
    /// Adds all players who have subscribed to updates for
    /// this condition with this result to the notification queue.
    /// </summary>
    [PublicAPI]
    public void RaisePreconditionBreakpoint(EntityUid target, int nodeId, int conditionIndex, bool result)
    {
        var netTarget = GetNetEntity(target);

        foreach (var (session, points) in Breakpoints)
        {
            for (var i = 0; i < points.Count; i++)
            {
                var point = points[i];

                if (point.Target != netTarget
                    || point.NodeId != nodeId
                    || point.Index != conditionIndex
                    || point.Kind != GoapBreakpointKind.Precondition)
                    continue;

                switch (result)
                {
                    case true
                        when point.Result != GoapBreakpointResultKind.True:
                    case false
                        when point.Result != GoapBreakpointResultKind.False:
                        continue;
                    default:
                        QueueDebugSend(session, target, point);
                        RemoveBreakpoint(session, point);
                        i--;
                        break;
                }
            }
        }
    }

    [PublicAPI]
    public void RaiseServiceBreakpoint(EntityUid target, int nodeId, int serviceIndex, bool result)
    {
        var netTarget = GetNetEntity(target);

        foreach (var (session, points) in Breakpoints)
        {
            for (var i = 0; i < points.Count; i++)
            {
                var point = points[i];

                if (point.Target != netTarget
                    || point.NodeId != nodeId
                    || point.Index != serviceIndex
                    || point.Kind != GoapBreakpointKind.Service)
                    continue;

                switch (result)
                {
                    case true
                        when point.Result != GoapBreakpointResultKind.True:
                    case false
                        when point.Result != GoapBreakpointResultKind.False:
                        continue;
                    default:
                        QueueDebugSend(session, target, point);
                        RemoveBreakpoint(session, point);
                        i--;
                        break;
                }
            }
        }
    }

    protected void RemoveBreakpoint(ICommonSession session, GoapBreakpoint point)
    {
        if (!Breakpoints.TryGetValue(session, out var points)
            || !points.Remove(point))
            return;

        RaiseNetworkEvent(new GoapBreakpointRemoveMessage(point), session);
    }

    #endregion
}

/// <summary>
/// Used to check GOAP conditions without losing the type of condition.
/// </summary>
public interface IGoapConditionCheсker
{
    /// <summary>
    /// Checks whether the GOAP target entity satisfies the condition.
    /// </summary>
    /// <typeparam name="T">GOAP condition type./</typeparam>
    /// <param name="target">Target entity.</param>
    /// <param name="state">
    /// The state against which the check should be performed.
    /// It may differ from the agent's actual state.
    /// </param>
    /// <param name="condition">GOAP condition.</param>
    /// <param name="dump">Debug dump.</param>
    /// <returns>True, if the check is passed; otherwise, false</returns>
    bool CheckCondition<T>(
        EntityUid target,
        GoapState state,
        T condition,
        out GoapDebugDump? dump)
        where T : BaseGoapCondition<T>;

    /// <summary>
    /// Returns the value of a key from the agent's GOAP state.
    /// </summary>
    /// <remarks>
    /// This method differs from <see cref="GoapState.TryGetValue{T}(string, out T?)"/> in that it returns
    /// the default values for certain keys that are not present in the state,
    /// such as <see cref="GoapState.OwnerCoordinates"/>.
    /// </remarks>
    /// <typeparam name="T">Value type.</typeparam>
    /// <returns>true if the GoapState contains an element with the specified key; otherwise, false.</returns>
    bool TryGetValue<T>(
        GoapState state,
        string key,
        [NotNullWhen(true)] out T? value)
        where T : notnull;

    /// <inheritdoc cref="TryGetValue{T}(Content.Shared._RF.NPC.GOAP.GoapState,string,out T?)"/>
    bool TryGetValue<T>(
        GoapState state,
        StateKey<T> key,
        [NotNullWhen(true)] out T? value)

        where T : notnull;

    /// <summary>
    /// Returns the value of a key from the agent's GOAP state.
    /// </summary>
    /// <typeparam name="T">Value type.</typeparam>
    T GetValue<T>(GoapState state, string key) where T : notnull;

    /// <inheritdoc cref="GetValue{T}(Content.Shared._RF.NPC.GOAP.GoapState,string)"/>
    T GetValue<T>(GoapState state, StateKey<T> key) where T : notnull;
}

public interface IGoapActionPerformer
{
    /// <summary>
    /// Calculates the cost of executing a GOAP action.
    /// </summary>
    /// <typeparam name="T">GOAP action type.</typeparam>
    /// <param name="target">Target entity.</param>
    /// <param name="state">
    /// The state against which the calculation should be performed.
    /// It may differ from the agent's actual state.
    /// </param>
    /// <param name="action">GOAP action.</param>
    /// <returns>Action execution cost.</returns>
    float ActionCost<T>(EntityUid target, GoapState state, T action) where T : BaseGoapAction<T>;

    /// <summary>
    /// Updates the execution of a GOAP action.
    /// </summary>
    /// <typeparam name="T">GOAP action type.</typeparam>
    /// <param name="target">Target entity.</param>
    /// <param name="action">GOAP action.</param>
    /// <param name="dump">Debug dump.</param>
    /// <returns>Update result.</returns>
    GoapActionResult UpdateAction<T>(EntityUid target, T action, out GoapDebugDump? dump) where T : BaseGoapAction<T>;

    /// <summary>
    /// Starts the action.
    /// </summary>
    /// <typeparam name="T">GOAP action type.</typeparam>
    /// <param name="target">Target entity.</param>
    /// <param name="action">GOAP action.</param>
    /// <param name="dump">Debug dump.</param>
    /// <returns>True, if the action startup was successful.</returns>
    bool ActionStartup<T>(EntityUid target, T action, out GoapDebugDump? dump) where T : BaseGoapAction<T>;

    /// <summary>
    /// Finishes the action.
    /// </summary>
    /// <typeparam name="T">GOAP action type.</typeparam>
    /// <param name="target">Target entity.</param>
    /// <param name="action">GOAP action.</param>
    /// <param name="dump">Debug dump.</param>
    void ActionShutdown<T>(EntityUid target, T action, out GoapDebugDump? dump) where T : BaseGoapAction<T>;
}

public interface IGoapServiceChecker
{
    /// <param name="target">Target entity.</param>
    /// <param name="state">
    /// The state against which the check should be performed.
    /// It may differ from the agent's actual state.
    /// </param>
    /// <param name="service">GOAP service.</param>
    /// <param name="cancellation">A token for interrupting the asynchronous operation of the service.</param>
    /// <typeparam name="T">GOAP service type.</typeparam>
    /// <returns>
    /// A state that stores the results of the service's operation,
    /// or <b>null</b> if the service's operation failed.
    /// </returns>
    Task<GoapState?> CheckService<T>(
        EntityUid target,
        GoapState state,
        T service,
        CancellationToken cancellation = default) where T : BaseGoapService<T>;
}
