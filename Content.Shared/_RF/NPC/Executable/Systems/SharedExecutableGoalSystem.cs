using System.Linq;
using Content.Shared._RF.NPC.Executable.Components;
using Content.Shared._RF.NPC.Executable.Prototypes;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared._RF.NPC.UtilityAi;
using Content.Shared._RF.NPC.UtilityAi.Components;
using Content.Shared._RF.NPC.UtilityAi.Prototypes;
using Content.Shared._RF.NPC.UtilityAi.Systems;
using Content.Shared._RF.Selection.Systems;
using Content.Shared.CombatMode;
using Content.Shared.Maps;
using Content.Shared.NPC;
using Content.Shared.Physics;
using Content.Shared.Verbs;
using Content.Shared.Whitelist;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared._RF.NPC.Executable.Systems;

/// <summary>
/// A system that allows the player to set Utility AI goals for NPCs.
/// </summary>
public abstract partial class SharedExecutableGoalSystem : EntitySystem
{
    [Dependency] protected readonly IPrototypeManager Proto = default!;
    [Dependency] protected readonly EntityWhitelistSystem Whitelist = default!;
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] protected readonly SharedGoapSystem Goap = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedUtilityAiSystem _utilityAi = default!;
    [Dependency] private readonly SharedSelectionSystem _selection = default!;
    [Dependency] private readonly SharedCombatModeSystem _combatMode = default!;

    [Dependency] protected readonly EntityQuery<GoapComponent> GoapQuery = default!;
    [Dependency] protected readonly EntityQuery<ControllableNpcComponent> ControllableQuery = default!;
    [Dependency] protected readonly EntityQuery<NpcControllerComponent> ControllerQuery = default!;
    [Dependency] protected readonly EntityQuery<PassiveGoalTargetComponent> PassiveGoalQuery = default!;
    [Dependency] private readonly EntityQuery<ActiveNPCComponent> _activeQuery = default!;

    protected readonly Dictionary<ProtoId<UtilityAiGoalPrototype>, HashSet<ProtoId<ExecutableGoalPrototype>>>
        Executables = new();

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GetVerbsEvent<Verb>>(OnGetVerbs);
        SubscribeLocalEvent<ControllableNpcComponent, BeforeUtilityAiGoalFinished>(OnUtilityAiGoalFinished);

        SubscribeAllEvent<SetGoalRequest>(OnGoalRequest);
        SubscribeAllEvent<SetVerbGoalRequest>(OnSetVerbGoalRequest);
        SubscribeAllEvent<PassiveGoalRequest>(OnPassiveGoalRequest);
        SubscribeAllEvent<PassiveGoalRemoveRequest>(OnPassiveGoalRemoveRequest);
        SubscribeNetworkEvent<SetCombatModeMessage>(OnSetCombatModeMessage);

        Subs.ProtoReload<ExecutableGoalPrototype>(Proto, ReloadPrototypes);

        ReloadPrototypes();
    }

    #region Events Handle

    private void ReloadPrototypes()
    {
        Executables.Clear();

        foreach (var proto in Proto.EnumeratePrototypes<ExecutableGoalPrototype>())
        {
            if (!Executables.TryAdd(proto.Goal, new() { proto }))
                Executables[proto.Goal].Add(proto);
        }
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
                ClientExclusive = true,
                Act = () =>
                {
                    if (!Timing.IsFirstTimePredicted)
                        return;

                    RaisePredictiveEvent(new SetVerbGoalRequest(
                        entities.Select(x => GetNetEntity(x)).ToList(),
                        goal.ID,
                        GetNetEntity(ev.Target),
                        NeedForceGoalExecution()));
                },
            });
        }
    }

    private void OnSetVerbGoalRequest(SetVerbGoalRequest request, EntitySessionEventArgs args)
    {
        if (!Timing.IsFirstTimePredicted
            || args.SenderSession.AttachedEntity is not { } requester
            || !ControllerQuery.TryComp(requester, out var control)
            || !control.Goals.Contains(request.Goal))
            return;

        var target = GetEntity(request.Target);

        foreach (var netUid in request.Entities)
        {
            var uid = GetEntity(netUid);

            if (!CanControl(requester, uid))
                continue;

            if (request.Force)
            {
                if (TrySetGoal(uid, request.Goal, target))
                    ClearQueue(uid);
                continue;
            }

            TryAddToQueue(uid, request.Goal, requester, target: target);
        }
    }

    private void OnUtilityAiGoalFinished(Entity<ControllableNpcComponent> ent, ref BeforeUtilityAiGoalFinished args)
    {
        if (ent.Comp.CurrentGoal is { } exec
            && TryComp(ent, out GoapComponent? goap)
            && Proto.Resolve(exec, out var proto))
        {
            // Remove passive target when the goal successfully finished.
            if (args.Reason == UtilityAiGoalFinishReason.Finished
                && proto.GoalType.HasFlag(ExecutableGoalType.Passive)
                && SharedGoapSystem.TryGetValueNoEcsDefaults(goap.State, proto.TargetKey, out var target)
                && PassiveGoalQuery.TryComp(target, out var passive)
                && passive.Goal == exec)
                RemovePassiveTarget(target);

            _utilityAi.ReleaseCaptured(ent.Owner);
            Goap.RemoveKey(goap.State, proto.TargetCoordinatesKey);
            Goap.RemoveKey(goap.State, proto.TargetKey);

            ent.Comp.CurrentGoal = null;
            ent.Comp.CurrentTarget = null;
            ent.Comp.CurrentTargetCoordinates = null;
            DirtyField(ent, ent.Comp, nameof(ControllableNpcComponent.CurrentGoal));
            DirtyField(ent, ent.Comp, nameof(ControllableNpcComponent.CurrentTarget));
            DirtyField(ent, ent.Comp, nameof(ControllableNpcComponent.CurrentTargetCoordinates));
        }

        if (args.Handled || ent.Comp.Queue.Count == 0)
            return;

        if (args.Reason == UtilityAiGoalFinishReason.Failed && ent.Comp.ClearQueueOnFail)
        {
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

        DirtyField(ent.AsNullable(), nameof(ControllableNpcComponent.Queue));
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
                    if (TrySetGoal(uid, goal, targetCoords: _turf.GetTileCenter(tileRef)))
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
                if (TrySetGoal(uid, goal, targetCoords: tileCenter))
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

            RemovePassiveTarget(uid);
        }
    }

    private void OnSetCombatModeMessage(SetCombatModeMessage msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } player)
            return;

        var entities = GetEntityList(msg.Entities);

        foreach (var uid in entities)
        {
            TrySetCombatMode(player, uid, msg.Combat);
        }
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
            if (type != null && !proto.GoalType.HasFlag(type.Value))
                continue;

            if (proto.GoalType.HasFlag(ExecutableGoalType.Place)
                && controllable != null
                && controllable.Goals.Contains(proto)
                && Goap.CheckCondition(ent, proto.Conditions))
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
        var result = Goap.CheckCondition(ent, goal.Conditions);
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
            Goap.PlanShutdown(new(ent, goap), GoapPlanFinishReason.Interrupted);

        if (goap.Planning)
            SharedGoapSystem.CancelPlanning(new(ent, goap));

        DebugTools.Assert(proto.GoalType.HasFlag(ExecutableGoalType.Place) || coords == null);
        DebugTools.Assert(!proto.GoalType.HasFlag(ExecutableGoalType.Place) || target == null);

        if (target != null)
            goap.State.SetValue(proto.TargetKey, target.Value);
        if (coords != null)
            goap.State.SetValue(proto.TargetCoordinatesKey, coords.Value);

        ent.Comp3.CurrentGoal = proto;
        ent.Comp3.CurrentTarget = target;
        ent.Comp3.CurrentTargetCoordinates = coords;

        if (_net.IsServer)
        {
            DirtyField(ent, ent.Comp3, nameof(ControllableNpcComponent.CurrentGoal));
            DirtyField(ent, ent.Comp3, nameof(ControllableNpcComponent.CurrentTarget));
            DirtyField(ent, ent.Comp3, nameof(ControllableNpcComponent.CurrentTargetCoordinates));
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

/// <summary>
/// An event raised when a passive NPC goal has been set.
/// Raised both for user and target.
/// </summary>
/// <param name="Goal">Npc goal prototype.</param>
/// <param name="Target">Passive goal target.</param>
/// <param name="User">User who issued this goal.</param>
[PublicAPI]
public readonly record struct NpcPassiveGoalSet(
    ProtoId<ExecutableGoalPrototype> Goal,
    EntityUid Target,
    EntityUid User);

/// <summary>
/// An event raised when a passive NPC goal has been removed.
/// </summary>
/// <param name="Goal">Npc goal prototype.</param>
/// <param name="Target">Passive goal target.</param>
/// <param name="User">User who issued this goal.</param>
[PublicAPI]
public readonly record struct NpcPassiveGoalRemoved(
    ProtoId<ExecutableGoalPrototype> Goal,
    EntityUid Target,
    EntityUid User);

[Serializable, NetSerializable]
public sealed class SetGoalRequest : EntityEventArgs
{
    public List<NetEntity> Entities { get; set; } = new();
    public NetCoordinates TargetCoordinates;
    public bool AddToQueue;
}

/// <summary>
/// Sent by the client when a verb-issued goal (from the target context menu) is selected for one
/// or more controlled entities. Carries the whole request atomically - who, what goal, on what
/// target, and whether it should execute immediately (Force, decided from client-only input
/// state at the moment the verb was clicked) or be queued - so the assignment happens as a
/// single deterministic operation on both the predicting client and the authoritative server.
/// See SharedExecutableGoalSystem.OnSetVerbGoalRequest / OnGetVerbs.
/// </summary>
[Serializable, NetSerializable]
public sealed class SetVerbGoalRequest(
    List<NetEntity> entities,
    ProtoId<ExecutableGoalPrototype> goal,
    NetEntity? target,
    bool force) : EntityEventArgs
{
    public List<NetEntity> Entities = entities;
    public ProtoId<ExecutableGoalPrototype> Goal = goal;
    public NetEntity? Target = target;
    public bool Force = force;
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

[Serializable, NetSerializable]
public sealed class SetCombatModeMessage(List<NetEntity> entities, bool combat) : EntityEventArgs
{
    public List<NetEntity> Entities = entities;
    public bool Combat = combat;
}
