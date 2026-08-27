using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared._RF.NPC.Engagement.Components;
using Content.Shared._RF.NPC.Engagement.Prototypes;
using Content.Shared._RF.NPC.Engagement.Systems;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Prototypes;
using Content.Shared._RF.NPC.Search.Prototypes;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared.Buckle;
using Content.Shared.CombatMode;
using Content.Shared.Dataset;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Movement.Pulling.Systems;
using JetBrains.Annotations;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
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
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedNpcSearcherSystem _searcher = default!;
    [Dependency] private readonly SharedBuckleSystem _buckle = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly EngagementSystem _engagement = default!;
    [Dependency] private readonly SharedCombatModeSystem _combatMode = default!;

    protected readonly Dictionary<ICommonSession, List<GoapBreakpoint>> Breakpoints = new();
    protected readonly Dictionary<EntityUid, HashSet<ICommonSession>> DebugSubscriptions = new();
    protected readonly List<(ICommonSession Session, EntityUid Target, bool? Condition)> DebugSendQueue = new();
    protected FrozenDictionary<ProtoId<GoapCompoundPrototype>, GoapStaticGraph> StaticGraphs =
            new Dictionary<ProtoId<GoapCompoundPrototype>, GoapStaticGraph>().ToFrozenDictionary();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GoapComponent, ComponentAdd>(OnGoapAdded);
    }

    private static void OnGoapAdded(Entity<GoapComponent> ent, ref ComponentAdd ev)
    {
        ent.Comp.State.SetValue(GoapState.Owner, ent.Owner);
    }

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
        var owner = Owner(state);
        var ev = new GoapStateValueSet<T>(owner, key, value);
        RaiseLocalEvent(owner, ref ev, true);
    }

    /// <inheritdoc cref="GoapState.SetValue"/>
    [PublicAPI]
    public void SetValue<T>(Entity<GoapComponent> ent, StateKey<T> key, T value) where T : notnull
        => SetValue(ent.Comp.State, key, value);

    /// <inheritdoc cref="GoapState.Remove{T}(StateKey{T})"/>
    [PublicAPI]
    public bool RemoveKey<T>(GoapState state, StateKey<T> key)
        where T : notnull => RemoveKey(state, key, out _);

    /// <inheritdoc cref="GoapState.Remove{T}(StateKey{T}, out T?)"/>
    [PublicAPI]
    public bool RemoveKey<T>(
        GoapState state,
        StateKey<T> key,
        [NotNullWhen(true)] out T? value)
        where T : notnull
    {
        if (!state.Remove(key, out value))
            return false;

        var owner = Owner(state);
        var ev = new GoapStateValueRemove<T>(owner, key, value);
        RaiseLocalEvent(owner, ref ev, true);
        return true;
    }

    [PublicAPI, Pure]
    public static bool TryGetValueNoEcsDefaults<T>(
        GoapState state,
        StateKey<T> key,
        [NotNullWhen(true)] out T? value) where T : notnull
    {
        if (state.TryGetValue(key, out value))
            return true;

        foreach (var part in GoapState.GetOrParts(key))
        {
            if (state.TryGetValue(part, out value))
                return true;
        }

        return false;
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

        if (TryGetStateDefaults(state, key, out var @default))
        {
            value = (T)@default;
            return true;
        }

        foreach (var part in GoapState.GetOrParts(key))
        {
            if (state.TryGetValue(part, out value))
                return true;

            if (!TryGetStateDefaults(state, part, out var partDefault))
                continue;

            value = (T)partDefault;
            return true;
        }

        return false;
    }

    /// <inheritdoc/>
    [PublicAPI, Pure]
    public T GetValue<T>(GoapState state, StateKey<T> key) where T : notnull
    {
        if (state.TryGetValue(key, out var value))
            return value;

        if (TryGetStateDefaults(state, key, out var def))
            return (T)def;

        foreach (var part in GoapState.GetOrParts(key))
        {
            if (state.TryGetValue(part, out value))
                return value;

            if (TryGetStateDefaults(state, part, out var partDefault))
                return (T)partDefault;
        }

        throw new KeyNotFoundException();
    }

    /// <inheritdoc cref="GetValue{T}(GoapState, StateKey{T})"/>
    [PublicAPI, Pure]
    public T GetValue<T>(Entity<GoapComponent> ent, StateKey<T> key) where T : notnull
        => GetValue(ent.Comp.State, key);

    [PublicAPI, Pure]
    public static EntityUid Owner(GoapState state) => state.GetValue(GoapState.Owner);

    private bool TryGetStateDefaults<T>(GoapState state, StateKey<T> key, [NotNullWhen(true)] out object? value)
        where T : notnull
    {
        value = default;

        if (!state.UseEntityDefaults)
            return false;

        var owner = Owner(state);

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

        if (key.Equals(GoapState.CombatMode))
        {
            value = _combatMode.IsInCombatMode(owner);
            return true;
        }

        if (TryGetStateDomain(state, key, out value))
            return true;

        return false;
    }

    private bool TryGetStateDomain<T>(GoapState state, StateKey<T> key, [NotNullWhen(true)] out object? value)
        where T : notnull
    {
        value = null;
        var owner = Owner(state);
        var domains = GoapState.GetDomainParts(key);

        if (domains.Length < 2)
            return false;

        if (GoapState.QueryDomain.TryGetParams(domains, out ProtoId<SearchQueryPrototype>? query))
        {
            if (!_proto.HasIndex(query)
                || !_searcher.TryGetBestResult(owner, state, query.Value, out var result))
                return false;

            value = result.Value;
            return true;
        }

        if (GoapState.QueryAllDomain.TryGetParams(domains, out query))
        {
            if (!_proto.HasIndex(query))
                return false;

            var results = _searcher.GetResults(owner, state, query.Value);

            if (results.Count == 0)
                return false;

            value = (T)results;
            return true;
        }

        // Engagements

        if (GoapState.InAnyEngagementDomain.Equals(domains))
        {
            value = TryComp(owner, out EngagementParticipantComponent? participant) && participant.Membership.Count > 0;
            return true;
        }

        if (GoapState.InEngagementDomain.TryGetParams(domains, out ProtoId<EngagementPrototype>? engagement))
        {
            if (!_proto.HasIndex(engagement))
                return false;

            value = _engagement.TryGetEngagement(owner, engagement.Value, out _, out _);
            return true;
        }

        if (GoapState.EngagementDomain.TryGetParams(domains, out engagement))
        {
            if (!_proto.HasIndex(engagement)
                || !_engagement.TryGetEngagement(owner, engagement.Value, out var ent, out _))
                return false;

            value = ent.Value.Owner;
            return true;
        }

        if (GoapState.EngagementStartedDomain.TryGetParams(domains, out engagement))
        {
            if (!_proto.HasIndex(engagement)
                || !_engagement.TryGetEngagement(owner, engagement.Value, out var ent, out _)
                || !ent.Value.Comp.Started)
                return false;

            value = ent.Value.Owner;
            return true;
        }

        if (GoapState.InEngagementRoleDomain.TryGetParams(domains, out engagement, out ProtoId<EngagementRolePrototype>? role))
        {
            if (!_proto.HasIndex(engagement) || !_proto.HasIndex(role))
                return false;

            value = _engagement.TryFindEngagement(owner, role.Value, engagement.Value, out _);
            return true;
        }

        if (GoapState.EngagementRoleDomain.TryGetParams(domains, out engagement, out role))
        {
            if (!_proto.HasIndex(engagement)
                || !_proto.HasIndex(role)
                || !_engagement.TryGetEngagement(owner, engagement.Value, out var ent, out _)
                || !_engagement.TryGetActors(ent.Value.AsNullable(), role.Value, out var actors)
                || actors.Count == 0)
                return false;

            value = actors.First();
            return true;
        }

        if (GoapState.EngagementInvitedDomain.TryGetParams(domains, out engagement))
        {
            if (!_proto.HasIndex(engagement)
                || !_engagement.TryGetInviteEngagement(owner, engagement.Value, out var ent))
                return false;

            value = ent.Value.Owner;
            return true;
        }

        if (GoapState.EngagementInvitedInviterDomain.TryGetParams(domains, out engagement))
        {
            if (!_proto.HasIndex(engagement)
                || !_engagement.TryGetInviteInviter(owner, engagement.Value, out var ent))
                return false;

            value = ent.Value;
            return true;
        }

        if (GoapState.EngagementInvitedRoleDomain.TryGetParams(domains, out engagement, out role))
        {
            if (!_proto.HasIndex(engagement)
                || !_proto.HasIndex(role)
                || !_engagement.TryGetInviteEngagement(owner, engagement.Value, out var ent, role))
                return false;

            value = ent.Value.Owner;
            return true;
        }

        if (GoapState.EngagementInvitedRoleInviterDomain.TryGetParams(domains, out engagement, out role))
        {
            if (!_proto.HasIndex(engagement)
                || !_proto.HasIndex(role)
                || !_engagement.TryGetInviteInviter(owner, engagement.Value, out var ent, role))
                return false;

            value = ent.Value;
            return true;
        }

        if (GoapState.EngagementInvitesRoleInvitedDomain.TryGetParams(domains, out engagement, out role))
        {
            if (!_proto.HasIndex(engagement)
                || !_proto.HasIndex(role)
                || !_engagement.TryGetEngagement(owner, engagement.Value, out var ent, out _))
                return false;

            foreach (var invite in ent.Value.Comp.Invites)
            {
                if (invite.Role != role)
                    continue;

                value = invite.Uid;
                return true;
            }

            return false;
        }

        // Datasets

        if (GoapState.DatasetAllDomain.TryGetParams(domains, out ProtoId<DatasetPrototype>? dataset))
        {
            if (!_proto.TryIndex(dataset, out var proto))
                return false;

            value = proto.Values;
        }

        if (GoapState.DatasetRandomDomain.TryGetParams(domains, out dataset))
        {
            if (!_proto.TryIndex(dataset, out var proto))
                return false;

            value = _random.Pick(proto.Values);
        }

        if (GoapState.LocalizedDatasetAllDomain.TryGetParams(domains, out ProtoId<LocalizedDatasetPrototype>? localeDataset))
        {
            if (!_proto.TryIndex(localeDataset, out var proto))
                return false;

            value = proto.Values;
        }

        if (GoapState.LocalizedDatasetRandomDomain.TryGetParams(domains, out localeDataset))
        {
            if (!_proto.TryIndex(localeDataset, out var proto))
                return false;

            value = _random.Pick(proto.Values);
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
