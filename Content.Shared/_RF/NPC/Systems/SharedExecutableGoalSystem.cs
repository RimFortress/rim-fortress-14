using System.Linq;
using Content.Shared._RF.NPC.Components;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared._RF.NPC.Prototypes;
using Content.Shared._RF.NPC.UtilityAi;
using Content.Shared._RF.NPC.UtilityAi.Components;
using Content.Shared._RF.NPC.UtilityAi.Prototypes;
using Content.Shared._RF.NPC.UtilityAi.Systems;
using Content.Shared._RF.Selection.Systems;
using Content.Shared.Maps;
using Content.Shared.NPC;
using Content.Shared.Physics;
using Content.Shared.Verbs;
using Content.Shared.Whitelist;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared._RF.NPC.Systems;

/// <summary>
/// A system that allows the player to set Utility AI goals for NPCs.
/// </summary>
public abstract partial class SharedExecutableGoalSystem : EntitySystem
{
    [Dependency] protected readonly IPrototypeManager Proto = default!;
    [Dependency] protected readonly EntityWhitelistSystem Whitelist = default!;
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedUtilityAiSystem _utilityAi = default!;
    [Dependency] private readonly SharedSelectionSystem _selection = default!;
    [Dependency] private readonly SharedGoapSystem _goap = default!;

    [Dependency] protected readonly EntityQuery<GoapComponent> GoapQuery = default!;
    [Dependency] protected readonly EntityQuery<ControllableNpcComponent> ControllableQuery = default!;
    [Dependency] protected readonly EntityQuery<NpcControllerComponent> ControllerQuery = default!;
    [Dependency] protected readonly EntityQuery<PassiveGoalTargetComponent> PassiveGoalQuery = default!;
    [Dependency] private readonly EntityQuery<ActiveNPCComponent> _activeQuery = default!;

    protected readonly Dictionary<ProtoId<UtilityAiGoalPrototype>, HashSet<ProtoId<ExecutableGoalPrototype>>>
        Executables = new();
    protected HashSet<ProtoId<ExecutableGoalPrototype>> IgnoreGoalsList = new();

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GetVerbsEvent<Verb>>(OnGetVerbs);
        SubscribeLocalEvent<ControllableNpcComponent, BeforeUtilityAiGoalFinished>(OnUtilityAiGoalFinished);
        SubscribeLocalEvent<NpcControllerComponent, PlayerAttachedEvent>(OnPlayerAttachedEvent);

        SubscribeAllEvent<SetGoalRequest>(OnGoalRequest);
        SubscribeAllEvent<PassiveGoalRequest>(OnPassiveGoalRequest);
        SubscribeAllEvent<PassiveGoalRemoveRequest>(OnPassiveGoalRemoveRequest);
        SubscribeAllEvent<ForceGoalExecutionMessage>(OnForceGoalExecutionMessage);
        SubscribeAllEvent<SetGoalMessage>(OnSetGoalMessage);
        SubscribeNetworkEvent<GoalTargetsClearedMessage>(OnGoalTargetsCleared);
        SubscribeNetworkEvent<GoalsIgnoreMessage>(OnGoalsIgnoreMessage);

        Proto.PrototypesReloaded += args =>
        {
            if (args.WasModified<ExecutableGoalPrototype>())
                ReloadPrototypes();
        };

        ReloadPrototypes();
    }

    #region Events Handle

    private void ReloadPrototypes()
    {
        Executables.Clear();
        IgnoreGoalsList.Clear();

        foreach (var proto in Proto.EnumeratePrototypes<ExecutableGoalPrototype>())
        {
            if (!Executables.TryAdd(proto.Goal, new() { proto }))
                Executables[proto.Goal].Add(proto);

            if (proto.Conditions.Count > 0)
                IgnoreGoalsList.Add(proto);

            if (Proto.Resolve(proto.Goal, out var goal) && goal.Conditions.Count > 0)
                IgnoreGoalsList.Add(proto);
        }

        if (_net.IsServer)
            RaiseNetworkEvent(new GoalsIgnoreMessage(IgnoreGoalsList));
    }

    private void OnGetVerbs(GetVerbsEvent<Verb> ev)
    {
        if (!TryComp(ev.User, out NpcControllerComponent? control))
            return;

        var tasks = new Dictionary<ExecutableGoalPrototype, List<EntityUid>>();
        var prototypes = control.Goals.Select(Proto.Index).ToList();

        foreach (var entity in _selection.SelectedEntities(ev.User))
        {
            if (!CanControl(ev.User, entity)
                || FindSatisfiedGoals(entity, ev.Target, prototypes, ExecutableGoalType.Verb) is not { } suitable)
                continue;

            foreach (var task in suitable)
            {
                if (!tasks.TryAdd(task, new()))
                    tasks[task].Add(entity);
                else
                    tasks[task] = new() { entity };
            }
        }

        foreach (var (goal, entities) in tasks)
        {
            ev.Verbs.Add(new()
            {
                Text = Loc.GetString(Proto.Index(goal.Goal).Name),
                Icon = goal.VerbIcon,
                Category = VerbCategory.NpcTask,
                CloseMenu = true,
                Act = () =>
                {
                    foreach (var uid in entities)
                    {
                        if (!TryAddToQueue(uid, goal, ev.User, target: ev.Target))
                            continue;

                        if (_net.IsClient && Timing.IsFirstTimePredicted && NeedForceGoalExecution())
                            RaisePredictiveEvent(new ForceGoalExecutionMessage(GetNetEntity(uid)));
                    }
                },
            });
        }
    }

    private void OnUtilityAiGoalFinished(Entity<ControllableNpcComponent> ent, ref BeforeUtilityAiGoalFinished args)
    {
        // In a situation where an agent has completed an executable goal and there is another one in the queue,
        // a situation may occur where we send a message to the client
        // in a single tick to clear the old goal and issue a new one,
        // which could lead to a race of information and visual bugs on the client.
        // So we clear the old goal only if the new one hasn't been assigned.
        Action? notifyClient = null;

        if (Executables.TryGetValue(args.Goal, out var execs)
            && TryComp(ent, out GoapComponent? goap))
        {
            foreach (var exec in execs)
            {
                if (!ent.Comp.Goals.Contains(exec)
                    || !Proto.Resolve(exec, out var proto))
                    continue;

                // Remove passive target when the goal successfully finished.
                if (args.Reason == UtilityAiGoalFinishReason.Finished
                    && proto.GoalType.HasFlag(ExecutableGoalType.Passive)
                    && goap.State.TryGetValue(proto.TargetKey, out var target)
                    && PassiveGoalQuery.TryComp(target, out var passive)
                    && passive.Goal == exec)
                    RemComp(target, passive);

                // The state changes above only happen on this side. The
                // client has its own copy of GoapState with the same target/coordinates, set
                // earlier via SetGoalMessage, and has no other way of knowing it's now stale.
                notifyClient = () =>
                {
                    if (!_net.IsServer)
                        return;

                    foreach (var owner in ent.Comp.CanControl)
                    {
                        RaiseNetworkEvent(new GoalTargetsClearedMessage
                            {
                                Agent = GetNetEntity(ent),
                                Goal = exec,
                                Target = goap.State.TryGetValue(proto.TargetKey, out var uid) ? GetNetEntity(uid) : null,
                                TargetCoordinates = goap.State.TryGetValue(proto.TargetCoordinatesKey, out var coords)
                                    ? GetNetCoordinates(coords)
                                    : null,
                            },
                            owner);
                    }
                };

                goap.State.Remove(proto.TargetCoordinatesKey);
                goap.State.Remove(proto.TargetKey);
                break;
            }
        }

        if (args.Handled || ent.Comp.Queue.Count == 0)
        {
            notifyClient?.Invoke();
            return;
        }

        if (args.Reason == UtilityAiGoalFinishReason.Failed && ent.Comp.ClearQueueOnFail)
        {
            notifyClient?.Invoke();
            ClearQueue(ent.AsNullable());
            return;
        }

        while (ent.Comp.Queue.TryDequeue(out var entry))
        {
            if (!TrySetGoal(ent.Owner, entry))
                continue;

            args.Handled = true;
            DirtyField(ent.AsNullable(), nameof(ControllableNpcComponent.Queue));
            return;
        }

        notifyClient?.Invoke();
        DirtyField(ent.AsNullable(), nameof(ControllableNpcComponent.Queue));
    }

    private void OnPlayerAttachedEvent(EntityUid uid, NpcControllerComponent component, PlayerAttachedEvent ev)
    {
        if (_net.IsServer)
            RaiseNetworkEvent(new GoalsIgnoreMessage(IgnoreGoalsList), ev.Player);
    }

    private void OnGoalTargetsCleared(GoalTargetsClearedMessage msg, EntitySessionEventArgs args)
    {
        if (!GoapQuery.TryComp(GetEntity(msg.Agent), out var comp)
            || !Proto.Resolve(msg.Goal, out var goal))
            return;

        comp.State.Remove(goal.TargetCoordinatesKey);
        comp.State.Remove(goal.TargetKey);
    }

    private void OnGoalsIgnoreMessage(GoalsIgnoreMessage msg, EntitySessionEventArgs args)
    {
        if (_net.IsClient)
            IgnoreGoalsList = msg.Goals;
    }

    private void OnGoalRequest(SetGoalRequest request, EntitySessionEventArgs args)
    {
        if (!Timing.IsFirstTimePredicted
            || args.SenderSession.AttachedEntity is not { } requester
            || !ControllerQuery.TryComp(requester, out var control))
            return;

        var entities = request.Entities
            .Select(GetEntity)
            .Where(x => CanControl(requester, x))
            .ToList();
        var targetCoords = GetCoordinates(request.TargetCoordinates);
        var allGoals = control.Goals.Select(t => Proto.Index(t)).ToList();
        var previousTargets = new List<TileRef>();
        ExecutableGoalPrototype? goal = null;

        foreach (var entity in entities)
        {
            if (FindSatisfiedGoals(entity, null, allGoals, ExecutableGoalType.Place) is not { } satisfied)
                continue;

            goal = satisfied[0];
            break;
        }

        if (goal == null)
            return;

        foreach (var uid in entities)
        {
            if (previousTargets.Count == 0)
            {
                if (!TryComp(targetCoords.EntityId, out MapGridComponent? grid)
                    || !_map.TryGetTileRef(targetCoords.EntityId, grid, targetCoords, out var tileRef)
                    || _turf.IsTileBlocked(tileRef, CollisionGroup.Impassable ^ CollisionGroup.HighImpassable))
                    break;

                previousTargets.Add(tileRef);
                if (!request.AddToQueue)
                {
                    TrySetGoal(uid, goal, targetCoords: _turf.GetTileCenter(tileRef));
                    ClearQueue(uid);
                }
                else
                    TryAddToQueue(uid, goal, requester, targetCoords: _turf.GetTileCenter(tileRef));
                continue;
            }

            if (GetNeighborTile(previousTargets) is not { } tile)
                continue;

            previousTargets.Add(tile);

            var tileCenter = _turf.GetTileCenter(tile);
            if (!request.AddToQueue)
            {
                TrySetGoal(uid, goal, targetCoords: tileCenter);
                ClearQueue(uid);
            }
            else
                TryAddToQueue(uid, goal, requester, targetCoords: tileCenter);
        }
    }

    private void OnPassiveGoalRequest(PassiveGoalRequest request, EntitySessionEventArgs args)
    {
        if (!Timing.IsFirstTimePredicted || args.SenderSession.AttachedEntity is not { } uid)
            return;

        SetPassiveTarget(uid, request.GoalId, GetEntityList(request.Entities));
    }

    private void OnPassiveGoalRemoveRequest(PassiveGoalRemoveRequest request, EntitySessionEventArgs args)
    {
        if (!Timing.IsFirstTimePredicted
            || !ControllerQuery.HasComp(args.SenderSession.AttachedEntity))
            return;

        foreach (var uid in GetEntityList(request.Entities))
        {
            if (!PassiveGoalQuery.TryComp(uid, out var comp)
                || comp.User != args.SenderSession.AttachedEntity)
                continue;

            RemComp(uid, comp);
        }
    }

    private void OnForceGoalExecutionMessage(ForceGoalExecutionMessage msg, EntitySessionEventArgs args)
    {
        if (!Timing.IsFirstTimePredicted
            || !TryGetEntity(msg.Agent, out var entity)
            || args.SenderSession.AttachedEntity is not { } player
            || !CanControl(player, entity.Value)
            || !ControllableQuery.TryComp(entity, out var comp)
            || comp.Queue.Count == 0)
            return;

        var entry = comp.Queue.Last();
        TrySetGoal(entity.Value, entry);
        ClearQueue(entity.Value);
    }

    private void OnSetGoalMessage(SetGoalMessage msg, EntitySessionEventArgs args)
    {
        if (!GoapQuery.TryComp(GetEntity(msg.Agent), out var comp)
            || !Proto.Resolve(msg.Goal, out var goal))
            return;

        if (GetEntity(msg.Target) is { } uid)
            comp.State.SetValue(goal.TargetKey, uid);

        if (GetCoordinates(msg.TargetCoordinates) is { } coords)
            comp.State.SetValue(goal.TargetCoordinatesKey, coords);
    }

    #endregion

    protected virtual bool NeedForceGoalExecution() => false;

    /// <summary>
    /// Finds the suitable goals for the target from the goals list.
    /// </summary>
    protected List<ExecutableGoalPrototype>? FindSatisfiedGoals(
        Entity<GoapComponent?> ent,
        EntityUid? target,
        List<ExecutableGoalPrototype> goals,
        ExecutableGoalType? type = null)
    {
        List<ExecutableGoalPrototype>? zeroGoals = null;
        List<ExecutableGoalPrototype>? satisfied = null;

        // Needed below to check whether this specific NPC is allowed to perform a place
        // goal at all - CheckGoalStart does the same check for targeted goals, but place
        // goals have no target to run CheckGoalStart against.
        ControllableQuery.TryComp(ent, out var controllable);

        foreach (var proto in goals)
        {
            // The client should not handle the logic of goals with conditions
            if (_net.IsClient && IgnoreGoalsList.Contains(proto))
                continue;

            if (type != null && !proto.GoalType.HasFlag(type))
                continue;

            if (proto.GoalType.HasFlag(ExecutableGoalType.Place)
                && controllable != null
                && controllable.Goals.Contains(proto))
            {
                zeroGoals ??= new();
                zeroGoals.Add(proto);
            }

            if (target == null
                || ent == target && !proto.SelfPerform
                || !CheckGoalStart(ent, proto, target.Value))
                continue;

            satisfied ??= new();
            satisfied.Add(proto);
        }

        return satisfied ?? zeroGoals;
    }

    private bool CheckGoalStart(
        Entity<GoapComponent?, ControllableNpcComponent?> ent,
        ExecutableGoalPrototype goal,
        EntityUid target)
    {
        if (!Whitelist.IsWhitelistPassOrNull(goal.TargetWhitelist, target)
            || GoalPerformersCount(goal, target) >= goal.MaxPerformers
            || !GoapQuery.Resolve(ent, ref ent.Comp1)
            || !ControllableQuery.Resolve(ent, ref ent.Comp2)
            || !ent.Comp2.Goals.Contains(goal)
            || !_utilityAi.ConditionsMet(ent, goal.Goal))
            return false;

        // Set a temporary variables in GoapState to check conditions.
        ent.Comp1.State.SetValue(goal.TargetKey, target);
        var result = _goap.CheckCondition(ent, goal.Conditions);
        ent.Comp1.State.Remove(goal.TargetKey);
        return result;
    }

    /// <summary>
    /// Creates a new goal for the NPC.
    /// </summary>
    private void SetGoal(
        Entity<GoapComponent, UtilityAiComponent, ControllableNpcComponent> ent,
        ExecutableGoalPrototype proto,
        EntityUid? target = null,
        EntityCoordinates? coords = null,
        Dictionary<string, object>? additionalKeys = null)
    {
        var goap = ent.Comp1;
        var utilityAi = ent.Comp2;

        if (goap.Plan != null)
            _goap.PlanShutdown(new(ent, goap), GoapPlanFinishReason.Interrupted);

        DebugTools.Assert(proto.GoalType.HasFlag(ExecutableGoalType.Place) || coords == null);
        DebugTools.Assert(!proto.GoalType.HasFlag(ExecutableGoalType.Place) || target == null);

        if (target != null)
            goap.State.SetValue(proto.TargetKey, target.Value);
        if (coords != null)
            goap.State.SetValue(proto.TargetCoordinatesKey, coords.Value);

        if (_net.IsServer)
        {
            foreach (var owner in ent.Comp3.CanControl)
            {
                RaiseNetworkEvent(new SetGoalMessage
                    {
                        Agent = GetNetEntity(ent),
                        Goal = proto,
                        Target = GetNetEntity(target),
                        TargetCoordinates = GetNetCoordinates(coords),
                    },
                    owner);
            }
        }

        if (additionalKeys != null)
        {
            foreach (var (id, obj) in additionalKeys)
            {
                goap.State.SetValue(id, obj);
            }
        }

        _utilityAi.SetGoal((ent, utilityAi, goap), proto.Goal);
    }

    /// <summary>
    /// Returns the first free neighboring tile for the tile list
    /// </summary>
    protected TileRef? GetNeighborTile(List<TileRef> tiles)
    {
        var directions = new[] { Vector2i.Left, Vector2i.Right, Vector2i.Up, Vector2i.Down };
        var indicates = tiles.Select(tile => tile.GridIndices).ToList();

        foreach (var tile in tiles)
        {
            foreach (var direction in directions)
            {
                var offsetCoords = tile.GridIndices + direction;

                if (indicates.Contains(offsetCoords))
                    continue;

                if (TryComp(tile.GridUid, out MapGridComponent? grid)
                    && _map.TryGetTileRef(tile.GridUid, grid, offsetCoords, out var tileRef)
                    && !_turf.IsTileBlocked(tileRef, CollisionGroup.Impassable ^ CollisionGroup.HighImpassable)
                    && !_turf.IsSpace(tileRef))
                    return tileRef;
            }
        }

        return null;
    }
}

/// <summary>
/// Invoked when a user who can control this entity is added.
/// </summary>
/// <param name="User">User entity.</param>
[PublicAPI]
public record struct NpcControllerAdded(EntityUid User);

[Serializable, NetSerializable]
public sealed class SetGoalRequest : EntityEventArgs
{
    public List<NetEntity> Entities { get; set; } = new();
    public NetCoordinates TargetCoordinates;
    public bool AddToQueue;
}

[Serializable, NetSerializable]
public sealed class SetGoalMessage : EntityEventArgs
{
    public NetEntity Agent;
    public ProtoId<ExecutableGoalPrototype>? Goal;
    public NetEntity? Target;
    public NetCoordinates? TargetCoordinates;
}

[Serializable, NetSerializable]
public sealed class PassiveGoalRequest(ProtoId<ExecutableGoalPrototype> goalId, List<NetEntity> entities) : EntityEventArgs
{
    public ProtoId<ExecutableGoalPrototype> GoalId { get; set; } = goalId;
    public List<NetEntity> Entities { get; set; } = entities;
}

[Serializable, NetSerializable]
public sealed class PassiveGoalRemoveRequest(List<NetEntity> entities) : EntityEventArgs
{
    public List<NetEntity> Entities { get; set; } = entities;
}

/// <summary>
/// Sent from server to client(s) when a finished goal's target/coordinates were removed
/// from the agent's GoapState, so the client can drop its own now-stale copy of them.
/// </summary>
[Serializable, NetSerializable]
public sealed class GoalTargetsClearedMessage : EntityEventArgs
{
    public NetEntity Agent;
    public ProtoId<ExecutableGoalPrototype> Goal;
    public NetEntity? Target;
    public NetCoordinates? TargetCoordinates;
}

/// <summary>
/// Sent to the client for notification; the logic determines which goals it should ignore.
/// </summary>
[Serializable, NetSerializable]
public sealed class GoalsIgnoreMessage(HashSet<ProtoId<ExecutableGoalPrototype>> goals) : EntityEventArgs
{
    public HashSet<ProtoId<ExecutableGoalPrototype>> Goals = goals;
}

/// <summary>
/// Sent by the client to notify the server that the last
/// target added to the agent's queue must be executed immediately.
/// </summary>
[Serializable, NetSerializable]
public sealed class ForceGoalExecutionMessage(NetEntity agent) : EntityEventArgs
{
    public NetEntity Agent = agent;
}
