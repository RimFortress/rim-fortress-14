using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server._RF.NPC.Components;
using Content.Server._RF.NPC.Prototypes;
using Content.Server.Construction;
using Content.Server.Construction.Components;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared._RF.Construction;
using Content.Shared._RF.NPC;
using Content.Shared.Database;
using Content.Shared.Maps;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC;
using Content.Shared.Physics;
using Content.Shared.Verbs;
using Content.Shared.Whitelist;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._RF.NPC.Systems;

/// <summary>
/// Manages <see cref="NpcControlComponent"/>
/// </summary>
public sealed class NpcControlSystem : SharedNpcControlSystem
{
    [Dependency] private readonly HTNSystem _htn = default!;
    [Dependency] private readonly NPCSystem _npc = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly NPCUtilitySystem _npcUtility = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;

    private EntityQuery<ControllableNpcComponent> _controllableQuery;
    private EntityQuery<PassiveNpcTaskTargetComponent> _passiveTaskQuery;
    private EntityQuery<ActiveNpcTaskTargetComponent> _targetsQuery;
    private EntityQuery<HTNComponent> _htnQuery;

    /// <summary>
    /// A temporary list of user-selected entities, for the needs of verb tasks.
    /// </summary>
    // This should be implemented via NetworkComponent, but I'm too lazy to do that
    private readonly Dictionary<EntityUid, List<EntityUid>> _selected = new();

    /// <summary>
    /// Temporarily stores a list of all task planning failures for the task failure timer
    /// </summary>
    private readonly Dictionary<EntityUid, TimeSpan> _fails = new();

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<NpcTaskRequest>(OnTaskRequest);
        SubscribeNetworkEvent<PassiveNpcTaskRequest>(OnPassiveTaskRequest);
        SubscribeNetworkEvent<PassiveNpcTaskRemoveRequest>(OnPassiveTaskRemoveRequest);
        SubscribeNetworkEvent<AllowedNpcTasksInfoRequest>(OnAllowedTasksInfoRequest);

        SubscribeLocalEvent<ConstructionComponent, ConstructionChangeEntityEvent>(OnEntityChange);
        SubscribeLocalEvent<CommonConstructionGhostComponent, ConstructionChangeEntityEvent>(OnEntityChange);
        SubscribeLocalEvent<ControllableNpcComponent, ComponentRemove>(OnComponentRemove);
        SubscribeLocalEvent<ActiveNPCComponent, ComponentRemove>(OnComponentRemove);
        SubscribeLocalEvent<GetVerbsEvent<Verb>>(OnGetVerbs);
        SubscribeLocalEvent<HtnPlanningFailed>(OnPlanningFailed);

        _prototype.PrototypesReloaded += args =>
        {
            if (args.WasModified<NpcTaskPrototype>())
                ReloadPrototypes();
        };

        _controllableQuery = GetEntityQuery<ControllableNpcComponent>();
        _passiveTaskQuery = GetEntityQuery<PassiveNpcTaskTargetComponent>();
        _targetsQuery = GetEntityQuery<ActiveNpcTaskTargetComponent>();
        _htnQuery = GetEntityQuery<HTNComponent>();

        ReloadPrototypes();
    }

    #region Events Handle

    private void OnTaskRequest(NpcTaskRequest request, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } requester)
            return;

        var entities = request.Entities.Select(GetEntity).ToList();
        var target = GetEntity(request.Target);
        var targetCoords = GetCoordinates(request.TargetCoordinates);

        if (!TryComp(requester, out NpcControlComponent? control))
            return;

        var allTasks = control.Tasks.Select(t => _prototype.Index(t)).ToList();
        var previousTargets = new List<TileRef>();
        _selected[requester] = new();

        var verb = false;
        var satisfiedTasks = new Dictionary<EntityUid, List<NpcTaskPrototype>>();

        foreach (var entity in entities)
        {
            if (!CanControl(requester, entity)
                || FindSatisfiedTasks(entity, target, allTasks) is not { } satisfied)
                continue;

            satisfiedTasks.Add(entity, satisfied);
            _selected[requester].Add(entity);

            if (satisfied.Count > 1 || satisfied.FirstOrDefault(x => x.VerbOnly) != null)
                verb = true;
        }

        // If there is more than one suitable task for at least one entity, call the context menu
        if (verb && target != null)
        {
            RaiseNetworkEvent(new NpcTasksContextMenuMessage(GetNetEntity(target.Value)), requester);
            return;
        }

        _selected.Remove(requester);

        foreach (var (entity, tasks) in satisfiedTasks)
        {
            var task = tasks[0];

            if (task.TargetWhitelist != null && target != null)
            {
                TrySetTask(entity, task, target.Value);
                continue;
            }

            if (previousTargets.Count == 0)
            {
                if (!TryComp(targetCoords.EntityId, out MapGridComponent? grid)
                    || !_map.TryGetTileRef(targetCoords.EntityId, grid, targetCoords, out var tileRef)
                    || _turf.IsTileBlocked(tileRef, CollisionGroup.Impassable ^ CollisionGroup.HighImpassable))
                    break;

                previousTargets.Add(tileRef);

                var tileCoords = _turf.GetTileCenter(tileRef);
                TrySetTask(entity, task, tileCoords);
                continue;
            }

            if (GetNeighborTile(previousTargets) is not { } tile)
                continue;

            previousTargets.Add(tile);

            var tileCenter = _turf.GetTileCenter(tile);
            TrySetTask(entity, task, tileCenter);
        }
    }

    private void OnPassiveTaskRequest(PassiveNpcTaskRequest request, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } requester)
            return;

        var task = _prototype.Index<NpcTaskPrototype>(request.TaskId);
        var entities = request.Entities.Select(GetEntity).ToList();

        SetPassiveTaskTarget(requester, task, entities);
    }

    private void OnPassiveTaskRemoveRequest(PassiveNpcTaskRemoveRequest request, EntitySessionEventArgs args)
    {
        if (!TryComp(args.SenderSession.AttachedEntity, out NpcControlComponent? _))
            return;

        var removed = new List<EntityUid>();

        foreach (var netUid in request.Entities)
        {
            var uid = GetEntity(netUid);
            if (!_passiveTaskQuery.TryComp(uid, out var comp))
                continue;

            EntityManager.RemoveComponent(uid, comp);
            removed.Add(uid);
        }

        var msg = new PassiveNpcTaskRemoveMessage(removed.Select(x => GetNetEntity(x)).ToList());
        RaiseNetworkEvent(msg, args.SenderSession);
    }

    private void OnAllowedTasksInfoRequest(AllowedNpcTasksInfoRequest request, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } requester
            || !TryComp(requester, out NpcControlComponent? control))
            return;

        var info = control.Tasks
            .Select(x => _prototype.Index(x))
            .Where(x => x.Passive && control.Tasks.Contains(x))
            .Select(x => NpcTaskInfo(requester, x))
            .ToList();

        var msg = new AllowedNpcTasksInfoMessage(info);
        RaiseNetworkEvent(msg, requester);
    }

    // Help construction NPCs keep up-to-date information on the entity to be built
    private void OnEntityChange(EntityUid uid, IComponent comp, ConstructionChangeEntityEvent ev)
    {
        if (!_targetsQuery.TryComp(ev.Old, out var target))
            return;

        var newTarget = EnsureComp<ActiveNpcTaskTargetComponent>(ev.New);

        foreach (var (task, entities) in target.Tasks)
        {
            newTarget.Tasks[task] = entities;

            foreach (var entity in entities)
            {
                if (!_htnQuery.TryComp(entity, out var htn)
                    || !_controllableQuery.TryComp(entity, out var control))
                    continue;

                var proto = _prototype.Index(task);
                htn.Blackboard.SetValue(proto.TargetKey, ev.New);
                control.TaskTarget = ev.New;

                foreach (var user in control.CanControl)
                {
                    RaiseNetworkEvent(NpcTaskInfo(entity, proto, ev.New), user);
                }
            }
        }
    }

    private void OnGetVerbs(GetVerbsEvent<Verb> ev)
    {
        if (!TryComp(ev.User, out NpcControlComponent? control)
            || !_selected.TryGetValue(ev.User, out var selected))
            return;

        var tasks = new Dictionary<NpcTaskPrototype, List<EntityUid>>();
        var prototypes = control.Tasks.Select(_prototype.Index).ToList();

        foreach (var entity in selected)
        {
            if (FindSatisfiedTasks(entity, ev.Target, prototypes) is not { } suitable)
                continue;

            foreach (var task in suitable)
            {
                if (tasks.TryGetValue(task, out var value))
                    value.Add(entity);
                else
                    tasks.Add(task, new() { entity });
            }
        }

        foreach (var (task, entities) in tasks)
        {
            ev.Verbs.Add(new()
            {
                Text = task.Name,
                Icon = task.VerbIcon,
                Message = task.Description,
                Category = VerbCategory.NpcTask,
                Impact = LogImpact.Low,
                Act = () =>
                {
                    entities.ForEach(entity =>
                    {
                        if (task.TargetWhitelist != null)
                            TrySetTask(entity, task, ev.Target);
                        else
                            TrySetTask(entity, task, Transform(ev.Target).Coordinates);
                    });

                    _selected.Remove(ev.User);
                },
            });
        }
    }

    private void OnPlanningFailed(HtnPlanningFailed ev)
    {
        if (_fails.ContainsKey(ev.Entity)
            || !_controllableQuery.TryComp(ev.Entity, out var control)
            || !_prototype.TryIndex(control.CurrentTask, out var proto))
            return;

        // If the task planning fails, we start the countdown,
        // at the end of which we check again whether the task planning succeeded or not
        _fails[ev.Entity] = _timing.CurTime + proto.FailAwaitTime;
    }

    private void OnComponentRemove(EntityUid uid, IComponent component, ComponentRemove args)
    {
        FinishTask(uid);
    }

    #endregion

    private void ReloadPrototypes()
    {
        foreach (var proto in _prototype.EnumeratePrototypes<NpcTaskPrototype>())
        {
            foreach (var precondition in proto.FinishPreconditions)
            {
                precondition.Initialize(EntityManager.EntitySysManager);
            }

            foreach (var precondition in proto.StartPreconditions)
            {
                precondition.Initialize(EntityManager.EntitySysManager);
            }
        }
    }

    /// <summary>
    /// Finds the suitable tasks for the target from the task list
    /// </summary>
    private List<NpcTaskPrototype>? FindSatisfiedTasks(EntityUid uid, EntityUid? target, List<NpcTaskPrototype> tasks)
    {
        List<NpcTaskPrototype>? zeroTasks = null;
        List<NpcTaskPrototype>? satisfied = null;

        if (!_npc.TryGetNpc(uid, out var npc))
            return null;

        foreach (var proto in tasks)
        {
            if (proto.TargetWhitelist == null)
            {
                zeroTasks ??= new();
                zeroTasks.Add(proto);
            }

            if (target == null
                || uid == target && !proto.SelfPerform
                || !CheckTaskStart(npc.Blackboard, proto, target.Value))
                continue;

            satisfied ??= new();
            satisfied.Add(proto);
        }

        return satisfied ?? zeroTasks;
    }

    private bool CheckTaskStart(NPCBlackboard blackboard, NpcTaskPrototype task, EntityUid target)
    {
        if (!_whitelist.IsWhitelistPassOrNull(task.TargetWhitelist, target)
            || TaskPerformersCount(task, target) >= task.MaxPerformers)
            return false;

        // Set a temporary variables in NPCBlackboard to check conditions
        blackboard.SetValue(task.TargetKey, target);

        // Checking the fulfillment of additional starting conditions
        foreach (var condition in task.StartPreconditions)
        {
            if (condition.IsMet(blackboard))
                continue;

            blackboard.Remove<EntityUid>(task.TargetKey);
            return false;
        }

        blackboard.Remove<EntityUid>(task.TargetKey);
        return true;
    }

    /// <summary>
    /// Finds user who are currently performing a given NPC task.
    /// </summary>
    /// <param name="protoId">Target task prototype.</param>
    /// <param name="target">Entity, which is the target of a given task.</param>
    /// <param name="user">The first user found who is currently performing the task.</param>
    /// <returns>True, if user is found.</returns>
    public bool TryGetUser(
        ProtoId<NpcTaskPrototype> protoId,
        EntityUid target,
        [NotNullWhen(true)] out EntityUid? user)
    {
        user = null;
        TryGetUsers(protoId, target, out var users);
        user = users?.FirstOrDefault();
        return user != null;
    }

    /// <summary>
    /// Finds users who are currently performing a given NPC task.
    /// </summary>
    /// <param name="protoId">Target task prototype.</param>
    /// <param name="target">Entity, which is the target of a given task.</param>
    /// <param name="users">All users currently performing the task.</param>
    /// <returns>True, if users are found.</returns>
    public bool TryGetUsers(
        ProtoId<NpcTaskPrototype> protoId,
        EntityUid target,
        [NotNullWhen(true)] out HashSet<EntityUid>? users)
    {
        users = null;
        return _targetsQuery.TryComp(target, out var comp) && comp.Tasks.TryGetValue(protoId, out users);
    }

    /// <summary>
    /// Tries to set a new task for an NPC, checking all the required conditions
    /// </summary>
    /// <returns>True, if the task is successfully set</returns>
    public bool TrySetTask(
        Entity<HTNComponent?> npc,
        ProtoId<NpcTaskPrototype> protoId,
        EntityUid target,
        Dictionary<string, object>? additionalKeys = null)
        => _prototype.TryIndex(protoId, out var proto) && TrySetTask(npc, proto, target, additionalKeys);

    /// <summary>
    /// Tries to set a new task for an NPC, checking all the required conditions
    /// </summary>
    /// <returns>True, if the task is successfully set</returns>
    public bool TrySetTask(
        Entity<HTNComponent?> npc,
        NpcTaskPrototype proto,
        EntityUid target,
        Dictionary<string, object>? additionalKeys = null)
    {
        if (!Resolve(npc, ref npc.Comp)
            || !_controllableQuery.TryComp(npc, out var control)
            || !CheckTaskStart(npc.Comp.Blackboard, proto, target))
            return false;

        SetTask(
            new(npc.Owner, npc.Comp, control),
            proto,
            target,
            additionalKeys: additionalKeys);
        return true;
    }

    /// <summary>
    /// Tries to set a new task for an NPC
    /// </summary>
    /// <returns>True, if the task is successfully set</returns>
    public bool TrySetTask(
        Entity<HTNComponent?> npc,
        ProtoId<NpcTaskPrototype> protoId,
        EntityCoordinates? targetCoords,
        Dictionary<string, object>? additionalKeys = null)
        => _prototype.TryIndex(protoId, out var proto) && TrySetTask(npc, proto, targetCoords, additionalKeys);

    /// <summary>
    /// Tries to set a new task for an NPC
    /// </summary>
    /// <returns>True, if the task is successfully set</returns>
    public bool TrySetTask(
        Entity<HTNComponent?> npc,
        NpcTaskPrototype proto,
        EntityCoordinates? targetCoords,
        Dictionary<string, object>? additionalKeys = null)
    {
        if (!Resolve(npc, ref npc.Comp) || !_controllableQuery.TryComp(npc, out var control))
            return false;

        SetTask(
            new(npc.Owner, npc.Comp, control),
            proto,
            null,
            targetCoords,
            additionalKeys: additionalKeys);
        return true;
    }

    /// <summary>
    /// Tries to set a new task for an NPC
    /// </summary>
    /// <returns>True, if the task is successfully set</returns>
    public bool TrySetTask(
        Entity<HTNComponent?> npc,
        ProtoId<NpcTaskPrototype> protoId,
        Dictionary<string, object>? additionalKeys = null)
        => _prototype.TryIndex(protoId, out var proto) && TrySetTask(npc, proto, additionalKeys);

    /// <summary>
    /// Tries to set a new task for an NPC
    /// </summary>
    /// <returns>True, if the task is successfully set</returns>
    public bool TrySetTask(
        Entity<HTNComponent?> npc,
        NpcTaskPrototype proto,
        Dictionary<string, object>? additionalKeys = null)
    {
        if (!Resolve(npc, ref npc.Comp) || !_controllableQuery.TryComp(npc, out var control))
            return false;

        SetTask(new(npc.Owner, npc.Comp, control), proto, additionalKeys: additionalKeys);
        return true;
    }

    /// <summary>
    /// Creates a new task for the NPC
    /// </summary>
    private void SetTask(
        Entity<HTNComponent, ControllableNpcComponent> entity,
        NpcTaskPrototype proto,
        EntityUid? target = null,
        EntityCoordinates? coords = null,
        Dictionary<string, object>? additionalKeys = null)
    {
        var htn = entity.Comp1;
        var control = entity.Comp2;

        if (control.CurrentTask != null)
            FinishTask(new(entity, control, htn), TaskFinishStatus.Replaced);

        if (target != null)
            EnsureComp<ActiveNpcTaskTargetComponent>(target.Value).Tasks.GetOrNew(proto).Add(entity);

        if (htn.Plan != null)
        {
            _htn.ShutdownTask(htn.Plan.CurrentOperator, htn.Blackboard, HTNOperatorStatus.Failed);
            _htn.ShutdownPlan(htn);
            htn.Plan = null;
        }

        control.CurrentTask = proto;
        control.TaskTarget = target;

        if (target != null)
            htn.Blackboard.SetValue(proto.TargetKey, target);
        if (coords != null)
            htn.Blackboard.SetValue(proto.TargetCoordinatesKey, coords);

        if (additionalKeys != null)
        {
            foreach (var (id, obj) in additionalKeys)
            {
                htn.Blackboard.SetValue(id, obj);
            }
        }

        htn.RootTask = new HTNCompoundTask { Task = proto.Compound };
        _htn.Replan(htn);

        RaiseLocalEvent(entity, new NpcTaskGiven(proto, target, coords));

        if (target != null)
            RaiseLocalEvent(target.Value, new NpcTaskGivenTarget(proto, entity));

        // We notify only users who can control NPCs of the change,
        // so that players cannot know about tasks of other players
        foreach (var uid in control.CanControl)
        {
            RaiseNetworkEvent(NpcTaskInfo(entity, proto, target, coords), uid);
        }
    }

    /// <summary>
    /// Ends the current entity task and deletes all temporary keys and notifies the users
    /// </summary>
    public void FinishTask(
        Entity<ControllableNpcComponent?, HTNComponent?> npc,
        TaskFinishStatus status = TaskFinishStatus.Finished)
    {
        if (!Resolve(npc, ref npc.Comp1, false)
            || !Resolve(npc, ref npc.Comp2, false)
            || !_prototype.TryIndex(npc.Comp1.CurrentTask, out var proto))
            return;

        var control = npc.Comp1;
        var htn = npc.Comp2;

        RemoveActiveTarget(control.TaskTarget, control.CurrentTask!.Value, npc);

        if (control.TaskTarget != null
            && status == TaskFinishStatus.Finished
            && _passiveTaskQuery.TryComp(control.TaskTarget, out var comp)
            && comp.RemoveWhenFailed
            && comp.Task == control.CurrentTask)
        {
            RemComp(control.TaskTarget.Value, comp);
            var msg = new PassiveNpcTaskRemoveMessage(new() { GetNetEntity(control.TaskTarget.Value) });

            foreach (var user in control.CanControl)
            {
                RaiseNetworkEvent(msg, user);
            }
        }

        var target = control.TaskTarget;
        htn.RootTask = new HTNCompoundTask { Task = proto.OnFinish };
        control.CurrentTask = null;
        control.TaskTarget = null;
        var blackboard = htn.Blackboard;
        var reason = string.Join("\n", npc.Comp1.TaskFailReason);

        if (string.IsNullOrEmpty(reason))
            reason = null;

        npc.Comp1.TaskFailReason.Clear();

        // Remove temporary keys from HTNBlackboard
        if (proto.DeleteKeysOnFinish)
        {
            if (blackboard.ContainsKey(proto.TargetKey))
                blackboard.Remove<EntityUid>(proto.TargetKey);

            if (blackboard.ContainsKey(proto.TargetCoordinatesKey))
                blackboard.Remove<EntityCoordinates>(proto.TargetCoordinatesKey);
        }

        foreach (var key in proto.TempKeys)
        {
            if (blackboard.ContainsKey(key))
                blackboard.Remove(key);
        }

        foreach (var uid in control.CanControl)
        {
            RaiseNetworkEvent(new NpcTaskFinishMessage(proto.ID, GetNetEntity(npc)), uid);
        }

        RaiseLocalEvent(npc, new NpcTaskFinished(proto, target, status, reason));

        if (target != null)
            RaiseLocalEvent(target.Value, new NpcTaskFinishedTarget(proto, npc));
    }

    /// <summary>
    /// Returns the first free neighboring tile for the tile list
    /// </summary>
    private TileRef? GetNeighborTile(List<TileRef> tiles)
    {
        var directions = new[] {Vector2i.Left, Vector2i.Right, Vector2i.Up, Vector2i.Down};
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

    private void RemoveActiveTarget(EntityUid? target, ProtoId<NpcTaskPrototype> task, EntityUid user)
    {
        if (target == null
            || !_targetsQuery.TryComp(target.Value, out var active)
            || !active.Tasks.TryGetValue(task, out var users))
            return;

        users.Remove(user);

        if (users.Count == 0)
            active.Tasks.Remove(task);

        if (active.Tasks.Count == 0)
            RemComp(target.Value, active);
    }

    /// <summary>
    /// Checks if the user can control this NPC
    /// </summary>
    public bool CanControl(EntityUid user, EntityUid entity)
    {
        return _controllableQuery.TryComp(entity, out var control)
               && TryComp(entity, out MobStateComponent? mobState)
               && mobState.CurrentState == MobState.Alive
               && control.CanControl.Contains(user);
    }

    public bool CanControl(ICommonSession user, EntityUid entity)
    {
        return user.AttachedEntity is { } uid && CanControl(uid, entity);
    }

    /// <summary>
    /// Returns a list of all entities that can be controlled by this user
    /// </summary>
    public List<EntityUid> ControllableEntities(EntityUid user)
    {
        var uids = new List<EntityUid>();
        var query = EntityQueryEnumerator<ControllableNpcComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.CanControl.Contains(user))
                uids.Add(uid);
        }

        return uids;
    }

    public List<EntityUid> ControllableEntities(ICommonSession user)
    {
        return user.AttachedEntity is not { } uid ? new() : ControllableEntities(uid);
    }

    /// <summary>
    /// Counts the number of performers of tasks on the given target, including unified tasks
    /// </summary>
    public int TaskPerformersCount(NpcTaskPrototype task, EntityUid target)
    {
        if (!_targetsQuery.TryComp(target, out var active))
            return 0;

        var count = 0;
        var tasks = new List<ProtoId<NpcTaskPrototype>>(task.UnionPerformersWith) { task };

        foreach (var (protoId, users) in active.Tasks)
        {
            if (tasks.Contains(protoId))
                count += users.Count;
        }

        return count;
    }

    /// <summary>
    /// Creates a passive target for the NPC task.
    /// </summary>
    /// <param name="user">The user who issues the task.</param>
    /// <param name="protoId">Task prototype.</param>
    /// <param name="uid">An entity that will become a passive target.</param>
    /// <param name="removeWhenFailed">Will the target be removed if the attempt to complete the task fails.</param>
    /// <param name="additionalKeys">The keys that will be given to the user when this passive task begins.</param>
    [PublicAPI]
    public bool SetPassiveTaskTarget(
        Entity<NpcControlComponent?> user,
        ProtoId<NpcTaskPrototype> protoId,
        EntityUid uid,
        bool removeWhenFailed = true,
        Dictionary<string, object>? additionalKeys = null)
        => SetPassiveTaskTarget(user, protoId, new List<EntityUid> { uid }, removeWhenFailed, additionalKeys);

    /// <summary>
    /// Creates passive targets for NPC task.
    /// </summary>
    /// <param name="user">The user who issues the task.</param>
    /// <param name="protoId">Task prototype.</param>
    /// <param name="entities">Entities that will become passive targets.</param>
    /// <param name="removeWhenFailed">Will the target be removed if the attempt to complete the task fails.</param>
    /// <param name="additionalKeys">The keys that will be given to the user when this passive task begins.</param>
    [PublicAPI]
    public bool SetPassiveTaskTarget(
        Entity<NpcControlComponent?> user,
        ProtoId<NpcTaskPrototype> protoId,
        List<EntityUid> entities,
        bool removeWhenFailed = true,
        Dictionary<string, object>? additionalKeys = null)
    {
        if (!Resolve(user, ref user.Comp)
            || !user.Comp.Tasks.Contains(protoId)
            || !_prototype.Resolve(protoId, out var proto))
            return false;

        var response = new List<NetEntity>();
        var blackboard = new NPCBlackboard();

        foreach (var (id, obj) in additionalKeys ?? new())
        {
            blackboard.SetValue(id, obj);
        }

        foreach (var uid in entities)
        {
            if (_passiveTaskQuery.TryComp(uid, out var task) && task.Task == proto)
                continue;

            // We use the blank NPCBlackboard to test the starting conditions. Nothing can go wrong. Right?
            if (!CheckTaskStart(blackboard, proto, uid))
                continue;

            var comp = EnsureComp<PassiveNpcTaskTargetComponent>(uid);
            comp.Task = proto.ID;
            comp.User = user;
            comp.AdditionalKeys = additionalKeys ?? new();
            comp.RemoveWhenFailed = removeWhenFailed;

            response.Add(GetNetEntity(uid));
        }

        if (response.Count == 0)
            return false;

        var msg = new PassiveNpcTaskMessage(proto.ID, response);
        RaiseNetworkEvent(msg, user);
        return true;
    }

    /// <summary>
    /// Tries to find a task target for an NPC
    /// </summary>
    /// <returns>Target for the task, if found</returns>
    private EntityUid? GetPassiveTaskTarget(Entity<HTNComponent?> npc, NpcTaskPrototype task)
    {
        if (!Resolve(npc, ref npc.Comp))
            return null;

        EntityUid? target = null;
        var minDist = (float) int.MaxValue;
        var canControlCache = new Dictionary<EntityUid, bool>();
        var coords = Transform(npc).Coordinates;

        var query = EntityQueryEnumerator<TransformComponent, PassiveNpcTaskTargetComponent>();
        while (query.MoveNext(out var uid, out var targetXform, out var comp))
        {
            if (!canControlCache.ContainsKey(comp.User))
                canControlCache.Add(comp.User, CanControl(comp.User, npc));

            if (!canControlCache[comp.User]
                || comp.Task != task
                || !CheckTaskStart(npc.Comp.Blackboard, task, uid)
                || !coords.TryDistance(EntityManager, targetXform.Coordinates, out var distance)
                || distance >= minDist)
                continue;

            minDist = distance;
            target = uid;
        }

        if (target == null && task.TargetsQuery != null)
        {
            var utilityTarget = _npcUtility.GetEntities(npc.Comp.Blackboard, task.TargetsQuery).GetHighest();

            if (utilityTarget.IsValid())
                target = utilityTarget;
        }

        return target;
    }

    /// <summary>
    /// Tries to find a task target and issue a task with that target to an NPC
    /// </summary>
    /// <returns>True, if the task is successfully issued</returns>
    public bool TrySetPassiveTask(Entity<HTNComponent?> npc, NpcTaskPrototype task)
    {
        if (GetPassiveTaskTarget(npc, task) is not { } target)
            return false;

        return _passiveTaskQuery.TryComp(target, out var comp)
            ? TrySetTask(npc, task, target, comp.AdditionalKeys)
            : TrySetTask(npc, task, target);
    }

    /// <summary>
    /// Tries to find a task target and issue a task with that target to an NPC
    /// </summary>
    /// <returns>True, if the task is successfully issued</returns>
    public bool TrySetPassiveTask(Entity<HTNComponent?> npc, ProtoId<NpcTaskPrototype> protoId)
        => _prototype.TryIndex(protoId, out var proto) && TrySetPassiveTask(npc, proto);

    /// <summary>
    /// Return current NPC task
    /// </summary>
    public bool TryGetCurrentTask(Entity<ControllableNpcComponent?> npc,
        [NotNullWhen(true)] out ProtoId<NpcTaskPrototype>? task)
    {
        task = null;

        if (!Resolve(npc, ref npc.Comp))
            return false;

        task = npc.Comp.CurrentTask;
        return task != null;
    }

    /// <summary>
    /// Give the user access to control this NPC
    /// </summary>
    public void AddNpcControl(EntityUid user, EntityUid uid)
    {
        EnsureComp<NpcControlComponent>(user);
        var comp = EnsureComp<ControllableNpcComponent>(uid);
        comp.CanControl.Add(user);
        RaiseLocalEvent(uid, new NpcControllerAdded(user));
    }

    /// <summary>
    /// Remove the user access to control this NPC
    /// </summary>
    public bool RemoveNpcControl(EntityUid user, Entity<ControllableNpcComponent?> uid)
    {
        if (!Resolve(uid, ref uid.Comp) || !uid.Comp.CanControl.Remove(user))
            return false;

        RaiseLocalEvent(uid, new NpcControllerRemoved(user));
        return true;
    }

    /// <summary>
    /// Allows the player to issue the given task
    /// </summary>
    public void AddAllowedTask(Entity<NpcControlComponent?> user, ProtoId<NpcTaskPrototype> proto)
    {
        if (!Resolve(user, ref user.Comp))
            return;

        user.Comp.Tasks.Add(proto);
    }

    /// <summary>
    /// Forbids the player from issuing the given task
    /// </summary>
    public void RemoveAllowedTask(Entity<NpcControlComponent?> user, ProtoId<NpcTaskPrototype> proto)
    {
        if (!Resolve(user, ref user.Comp))
            return;

        user.Comp.Tasks.Remove(proto);
    }

    /// <summary>
    /// Adds the reason for the task failure
    /// </summary>
    public bool AddFailReason(Entity<ControllableNpcComponent?> ent, string reason)
    {
        if (!Resolve(ent, ref ent.Comp) || ent.Comp.CurrentTask == null)
            return false;

        return ent.Comp.TaskFailReason.Add(reason);
    }

    private NpcTaskInfoMessage NpcTaskInfo(
        EntityUid entity,
        NpcTaskPrototype task,
        EntityUid? target = null,
        EntityCoordinates? coordinates = null)
    {
        return new NpcTaskInfoMessage(
            task.ID,
            task.Name,
            task.Description,
            task.VerbIcon?.TexturePath.CanonPath,
            task.OverlayColor,
            GetNetEntity(entity),
            GetNetEntity(target),
            GetNetCoordinates(coordinates));
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<ControllableNpcComponent, HTNComponent>();
        while (query.MoveNext(out var uid, out var comp, out var htn))
        {
            if (!_prototype.TryIndex(comp.CurrentTask, out var proto))
                continue;

            if (comp.TaskFinishCheckTime > _timing.CurTime)
                continue;

            comp.TaskFinishCheckTime = _timing.CurTime + proto.FinishCheckRate;
            var needFinish = proto.FinishPreconditions.Count != 0;

            // Check if the conditions for task finishing are met
            foreach (var precondition in proto.FinishPreconditions)
            {
                if (precondition.IsMet(htn.Blackboard))
                    continue;

                needFinish = false;
                break;
            }

            if (!needFinish)
                continue;

            // Finishing the task
            if (htn.Plan != null)
            {
                _htn.ShutdownTask(htn.Plan.CurrentOperator, htn.Blackboard, HTNOperatorStatus.Failed);
                _htn.ShutdownPlan(htn);
                htn.Plan = null;
            }

            FinishTask(new(uid, comp, htn));
        }

        // Checking recently failed tasks
        foreach (var (uid, time) in _fails)
        {
            if (time > _timing.CurTime)
                continue;

            if (_htnQuery.TryComp(uid, out var htn)
                && _controllableQuery.TryComp(uid, out var comp)
                && htn.Plan == null)
                FinishTask(new(uid, comp, htn), TaskFinishStatus.Failed);

            _fails.Remove(uid);
        }
    }
}

/// <summary>
/// Raised when an NPC has completed its current task.
/// </summary>
[PublicAPI]
public record struct NpcTaskFinished(
    ProtoId<NpcTaskPrototype> Task,
    EntityUid? Target,
    TaskFinishStatus Status,
    string? Reason);

/// <summary>
/// Raised when an NPC task targeting the entity is completed.
/// </summary>
/// <param name="Task">Task prototype.</param>
/// <param name="User">The NPC who started performing the task.</param>
[PublicAPI]
public record struct NpcTaskFinishedTarget(ProtoId<NpcTaskPrototype> Task, EntityUid User);

/// <summary>
/// Raised when an NPC receives a task.
/// </summary>
/// <param name="Task">Task prototype.</param>
/// <param name="Target">The target entity of the task.</param>
/// <param name="TargetCoordinates">Target coordinates of the task.</param>
[PublicAPI]
public record struct NpcTaskGiven(ProtoId<NpcTaskPrototype> Task, EntityUid? Target, EntityCoordinates? TargetCoordinates);

/// <summary>
/// Raised when the entity becomes the target of an NPC task.
/// </summary>
/// <param name="Task">Task prototype.</param>
/// <param name="User">The NPC who started performing the task.</param>
[PublicAPI]
public record struct NpcTaskGivenTarget(ProtoId<NpcTaskPrototype> Task, EntityUid User);

/// <summary>
/// Raised when a user who can control the entity is added.
/// </summary>
/// <param name="User">The user who can now control this entity.</param>
[PublicAPI]
public record struct NpcControllerAdded(EntityUid User);

/// <summary>
/// Raised when the user who can control this entity is removed.
/// </summary>
/// <param name="User">User who can no longer control this entity.</param>
[PublicAPI]
public record struct NpcControllerRemoved(EntityUid User);

[PublicAPI]
public enum TaskFinishStatus : byte
{
    /// <summary>
    /// The task has been successfully completed.
    /// </summary>
    Finished,

    /// <summary>
    /// The task was replaced with another one ahead of schedule.
    /// </summary>
    Replaced,

    /// <summary>
    /// The task ended in failure.
    /// </summary>
    Failed,
}
