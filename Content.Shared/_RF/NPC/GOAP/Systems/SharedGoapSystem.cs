using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using Content.Shared._RF.Conversation.Components;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Prototypes;
using Content.Shared.Buckle;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Movement.Pulling.Systems;
using JetBrains.Annotations;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared._RF.NPC.GOAP.Systems;

/// <summary>
/// A system that handles all the logic of the Goal-Oriented Action Planning AI
/// and provides an API for working with it.
/// </summary>
public abstract class SharedGoapSystem : EntitySystem, IGoapConditionChecker, IGoapActionPerformer
{
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedBuckleSystem _buckle = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;

    protected readonly Dictionary<ICommonSession, List<GoapBreakpoint>> Breakpoints = new();
    protected readonly Dictionary<EntityUid, HashSet<ICommonSession>> DebugSubscriptions = new();
    protected readonly List<(ICommonSession Session, EntityUid Target, bool? Condition)> DebugSendQueue = new();
    protected FrozenDictionary<ProtoId<GoapCompoundPrototype>, GoapStaticGraph> StaticGraphs =
            new Dictionary<ProtoId<GoapCompoundPrototype>, GoapStaticGraph>().ToFrozenDictionary();

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
            comp.Plan != null,
            $"attempt to update action for an agent without a plan! Agent: {ToPrettyString(target)}, Action: {action.GetType().ToString()}");

        if (comp.PlanDebug == null)
            return result;

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
                    || point.NodeId != debugAction.NodeIndex && point.NodeId != -1
                    || point.Index != debugAction.ActionIndex && point.Index != -1
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
                        BreakpointHit(session, point, planDebug);
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
        return ev.Cost * action.CostMultiplier;
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
#if TOOLS
        var success = action.Startup(target, this, out var dump);
        var comp = Comp<GoapComponent>(target);
        DebugTools.Assert(
            comp.Plan != null,
            $"attempt to startup action for an agent without a plan! Agent: {ToPrettyString(target)}, Action: {action.GetType().ToString()}");

        if (comp.PlanDebug == null)
            return success;

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
                    || point.NodeId != debugAction.NodeIndex && point.NodeId != -1
                    || point.Index != debugAction.ActionIndex && point.Index != -1
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
                        BreakpointHit(session, point, planDebug);
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
            comp.Plan != null,
            $"attempt to shutdown action for an agent without a plan! Agent: {ToPrettyString(target)}, Action: {action.GetType().ToString()}");

        if (comp.PlanDebug == null)
            return;

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
                    || point.NodeId != debugAction.NodeIndex && point.NodeId != -1
                    || point.Index != debugAction.ActionIndex && point.Index != -1
                    || point.Kind != GoapBreakpointKind.ActionShutdown
                    || point.Result != GoapBreakpointResultKind.None)
                    continue;

                BreakpointHit(session, point, planDebug);
                RemoveBreakpoint(session, point);
                i--;
            }
        }
#else
        action.Shutdown(target, this, out _);
#endif
    }

    public void ActionPlanShutdown<T>(EntityUid target, T action, GoapPlanFinishReason reason, out GoapDebugDump? dump)
        where T : BaseGoapAction<T>
    {
        action.Dump = null;
        var ev = new GoapActionPlanShutdown<T>(action, reason);
        RaiseLocalEvent(target, ref ev);
        dump = action.Dump;
        action.Dump = null;
    }

    /// <summary>
    /// Finishes the action in plan.
    /// </summary>
    /// <param name="target">Target entity.</param>
    /// <param name="action">GOAP action.</param>
    /// <param name="reason">The reason the plan was completed.</param>
    protected void ActionPlanShutdown(EntityUid target, GoapAction action, GoapPlanFinishReason reason)
    {
#if TOOLS
        action.PlanShutdown(target, this, reason, out var dump);
        var comp = Comp<GoapComponent>(target);
        DebugTools.Assert(
            comp.Plan != null,
            $"attempt to shutdown action for an agent without a plan! Agent: {ToPrettyString(target)}, Action: {action.GetType().ToString()}");

        if (comp.PlanDebug == null)
            return;

        var plan = comp.Plan.Value;
        var planDebug = comp.PlanDebug.Value;
        DebugTools.Assert(plan.Actions.Count == planDebug.Actions.Count);
        planDebug.Actions[plan.Index] = planDebug.Actions[plan.Index].WithPlanShutdown(dump);

        // Sending debug information to users when a breakpoint is hit
        var debugAction = planDebug.Actions[plan.Index];
        var netTarget = GetNetEntity(target);
        foreach (var (session, points) in Breakpoints)
        {
            for (var i = 0; i < points.Count; i++)
            {
                var point = points[i];

                if (point.Target != netTarget
                    || point.NodeId != debugAction.NodeIndex && point.NodeId != -1
                    || point.Index != debugAction.ActionIndex && point.Index != -1
                    || point.Kind != GoapBreakpointKind.ActionPlanShutdown
                    || point.Result != GoapBreakpointResultKind.None)
                    continue;

                BreakpointHit(session, point, planDebug);
                RemoveBreakpoint(session, point);
                i--;
            }
        }
#else
        action.PlanShutdown(target, this, out _);
#endif
    }

    #endregion

    #region GoapState Proxy

    /// <inheritdoc cref="GoapState.SetValue"/>
    [PublicAPI]
    public void SetValue<T>(GoapState state, StateKey<T> key, T value) where T : notnull
    {
        state.SetValue(key, value);
        RaiseLocalEvent(state.GetValue(GoapState.Owner), new GoapStateValueSet<T>(key, value));
    }

    /// <inheritdoc cref="GoapState.Remove"/>
    [PublicAPI]
    public bool RemoveKey<T>(GoapState state, StateKey<T> key)
        where T : notnull => RemoveKey(state, key, out _);

    /// <inheritdoc cref="GoapState.Remove"/>
    [PublicAPI]
    public bool RemoveKey<T>(
        GoapState state,
        StateKey<T> key,
        [NotNullWhen(true)] out T? value)
        where T : notnull
    {
        if (!state.Remove(key, out value))
            return false;

        RaiseLocalEvent(state.GetValue(GoapState.Owner), new GoapStateValueSet<T>(key, value));
        return true;
    }

    /// <inheritdoc/>
    [PublicAPI, Pure]
    public bool TryGetValue<T>(
        GoapState state,
        StateKey<T> key,
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
    public T GetValue<T>(GoapState state, StateKey<T> key) where T : notnull
    {
        if (TryGetStateDefaults(state, key, out var value))
            return (T)value;

        return state.GetValue(key);
    }

    private bool TryGetStateDefaults<T>(GoapState state, StateKey<T> key, [NotNullWhen(true)] out object? value)
        where T : notnull
    {
        value = null;

        if (!state.UseEntityDefaults)
            return false;

        var owner = state.GetValue(GoapState.Owner);

        if (key.Equals(GoapState.OwnerCoordinates))
        {
            value = Transform(owner).Coordinates;
            return true;
        }

        if (key.Equals(GoapState.ActiveHand))
        {
            if (_hands.GetActiveHand(owner) is not { } hand)
                return false;

            value = hand;
            return true;
        }

        if (key.Equals(GoapState.InContainer))
        {
            value = _container.IsEntityInContainer(owner);
            return true;
        }

        if (key.Equals(GoapState.ActiveHandFree))
        {
            value = _hands.ActiveHandIsEmpty(owner);
            return true;
        }

        if (key.Equals(GoapState.ActiveHandEntity))
        {
            if (!_hands.TryGetActiveItem(owner, out var uid))
                return false;

            value = uid;
            return true;
        }

        if (key.Equals(GoapState.Buckled))
        {
            value = _buckle.IsBuckled(owner);
            return true;
        }

        if (key.Equals(GoapState.Pulled))
        {
            value = _pulling.IsPulled(owner);
            return true;
        }

        if (key.Equals(GoapState.FreeHandsCount))
        {
            value = _hands.CountFreeHands(owner);
            return true;
        }

        if (key.Equals(GoapState.InConversation))
        {
            value = HasComp<ConversationActorComponent>(owner);
            return true;
        }

        return false;
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
    public void PlanShutdown(Entity<GoapComponent> ent, GoapPlanFinishReason reason)
    {
        DebugTools.Assert(ent.Comp.Plan != null);

        ActionShutdown(ent, ent.Comp.Plan.Value.CurrentAction);

        for (var i = 0; i < ent.Comp.Plan.Value.Index + 1; i++)
        {
            ActionPlanShutdown(ent, ent.Comp.Plan.Value.Actions[i], reason);
        }

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

    #region Debug

    /// <summary>
    /// Sends debugging information about the AI to the client.
    /// </summary>
    /// <param name="session">Target client.</param>
    /// <param name="target">AI entity.</param>
    protected virtual void SendDebug(ICommonSession session, EntityUid target)
    {
        // Noop on client
    }

    /// <summary>
    /// Adds the player to the list of those who will receive debug information about the NPC's next plan.
    /// </summary>
    protected virtual void QueueDebugSend(ICommonSession session, EntityUid target, bool? condition = null)
    {
        // Noop on client
    }

    protected virtual void BreakpointHit(
        ICommonSession session,
        GoapBreakpoint breakpoint,
        GoapPlanDebugInfo plan)
    {
        // Noop on client
    }

    protected void RemoveBreakpoint(ICommonSession session, GoapBreakpoint point)
    {
        if (Breakpoints.TryGetValue(session, out var points) && points.Remove(point))
            RaiseNetworkEvent(new GoapBreakpointRemoveMessage(point), session);
    }

    #endregion
}

/// <summary>
/// Used to check GOAP conditions without losing the type of condition.
/// </summary>
public interface IGoapConditionChecker
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
    /// This method differs from <see cref="GoapState.TryGetValue{T}(StateKey{T}, out T?)"/> in that it returns
    /// the default values for certain keys that are not present in the state,
    /// such as <see cref="GoapState.OwnerCoordinates"/>.
    /// </remarks>
    /// <typeparam name="T">Value type.</typeparam>
    /// <returns>true if the GoapState contains an element with the specified key; otherwise, false.</returns>
    bool TryGetValue<T>(
        GoapState state,
        StateKey<T> key,
        [NotNullWhen(true)] out T? value)
        where T : notnull;

    /// <summary>
    /// Returns the value of a key from the agent's GOAP state.
    /// </summary>
    /// <typeparam name="T">Value type.</typeparam>
    T GetValue<T>(GoapState state, StateKey<T> key) where T : notnull;
}

/// <summary>
/// Used to work with GOAP actions without losing their type.
/// </summary>
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

    /// <summary>
    /// Notifies the action about the plan being finished.
    /// </summary>
    /// <typeparam name="T">GOAP action type.</typeparam>
    /// <param name="target">Target entity.</param>
    /// <param name="action">GOAP action.</param>
    /// <param name="reason">Plan finish reason.</param>
    /// <param name="dump">Debug dump.</param>
    void ActionPlanShutdown<T>(EntityUid target, T action, GoapPlanFinishReason reason, out GoapDebugDump? dump)
        where T : BaseGoapAction<T>;
}
