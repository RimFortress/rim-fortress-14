using System.Linq;
using Content.Server._RF.NPC.Components;
using Content.Server.Construction;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared._RF.NPC;
using Content.Shared.Database;
using Content.Shared.Maps;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Content.Shared.Verbs;
using Content.Shared.Whitelist;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._RF.NPC;

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
    private EntityQuery<HTNComponent> _htnQuery;
    private EntityQuery<TransformComponent> _xformQuery;

    /// <summary>
    /// A temporary list of user-selected entities, for the needs of verb tasks.
    /// </summary>
    // This should be implemented via NetworkComponent, but I'm too lazy to do that
    private readonly Dictionary<EntityUid, List<EntityUid>> _selected = new();

    /// <summary>
    /// A list of all specific tasks with a list of all entities that execute it,
    /// is needed to check the maximum number of task performers
    /// </summary>
    // Ideally this should also be stored in some component like NpcTaskTargetComponent, but I'm too lazy to do that
    private readonly Dictionary<(EntityUid? Entity, ProtoId<NpcTaskPrototype> Proto), List<EntityUid>> _tasks = new();

    /// <summary>
    /// Temporarily stores a list of all task planning failures for the task failure timer
    /// </summary>
    private readonly Dictionary<EntityUid, TimeSpan> _fails = new();

    /// <summary>
    /// Stores failures of passive tasks so that when you search for a target again,
    /// you don't have to perform a previously failed one
    /// </summary>
    private readonly List<EntityUid> _passiveTasksFails = new();

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<NpcTaskRequest>(OnTaskRequest);
        SubscribeNetworkEvent<PassiveNpcTaskRequest>(OnPassiveTaskRequest);
        SubscribeNetworkEvent<PassiveNpcTaskRemoveRequest>(OnPassiveTaskRemoveRequest);
        SubscribeNetworkEvent<AllowedNpcTasksInfoRequest>(OnAllowedTasksInfoRequest);

        SubscribeLocalEvent<ConstructionChangeEntityEvent>(OnEntityChange);
        SubscribeLocalEvent<GetVerbsEvent<Verb>>(OnGetVerbs);
        SubscribeLocalEvent<HtnPlanningFailed>(OnPlanningFailed);

        _prototype.PrototypesReloaded += args =>
        {
            if (args.WasModified<NpcTaskPrototype>())
                ReloadPrototypes();
        };

        _controllableQuery = GetEntityQuery<ControllableNpcComponent>();
        _passiveTaskQuery = GetEntityQuery<PassiveNpcTaskTargetComponent>();
        _htnQuery = GetEntityQuery<HTNComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();

        ReloadPrototypes();
    }

    #region Event Handle

    private void OnTaskRequest(NpcTaskRequest request)
    {
        var entities = request.Entities.Select(GetEntity).ToList();
        var requester = GetEntity(request.Requester);
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
        if (verb)
        {
            RaiseNetworkEvent(new NpcTasksContextMenuMessage(), requester);
            return;
        }

        _selected.Remove(requester);

        foreach (var (entity, tasks) in satisfiedTasks)
        {
            var task = tasks[0];

            if (task.TargetWhitelist != null)
            {
                TrySetTask(entity, task, target);
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
                TrySetTask(entity, task, null, tileCoords);
                continue;
            }

            if (GetNeighborTile(previousTargets) is not { } tile)
                continue;

            previousTargets.Add(tile);

            var tileCenter = _turf.GetTileCenter(tile);
            TrySetTask(entity, task, null, tileCenter);
        }
    }

    private void OnPassiveTaskRequest(PassiveNpcTaskRequest request)
    {
        var task = _prototype.Index<NpcTaskPrototype>(request.TaskId);
        var requester = GetEntity(request.Requester);
        var entities = request.Entities.Select(GetEntity).ToList();

        SetPassiveTaskTargets(requester, task, entities);
    }

    private void OnPassiveTaskRemoveRequest(PassiveNpcTaskRemoveRequest request)
    {
        var requester = GetEntity(request.Requester);
        var removed = new List<EntityUid>();

        if (!TryComp(requester, out NpcControlComponent? _))
            return;

        foreach (var netUid in request.Entities)
        {
            var uid = GetEntity(netUid);
            if (!_passiveTaskQuery.TryComp(uid, out var comp))
                continue;

            EntityManager.RemoveComponent(uid, comp);
            _passiveTasksFails.Remove(uid);
            removed.Add(uid);
        }

        var msg = new PassiveNpcTaskRemoveMessage(removed.Select(x => GetNetEntity(x)).ToList());
        RaiseNetworkEvent(msg, requester);
    }

    private void OnAllowedTasksInfoRequest(AllowedNpcTasksInfoRequest request)
    {
        var requester = GetEntity(request.Requester);

        if (!TryComp(requester, out NpcControlComponent? control))
            return;

        var info = control.Tasks
            .Select(x => _prototype.Index(x))
            .Where(x => x.Passive)
            .Select(x => NpcTaskInfo(requester, x))
            .ToList();

        var msg = new AllowedNpcTasksInfoMessage(info);
        RaiseNetworkEvent(msg, requester);
    }

    // Help construction NPCs keep up-to-date information on the entity to be built
    private void OnEntityChange(ConstructionChangeEntityEvent ev)
    {
        foreach (var (task, entities) in _tasks.ToList())
        {
            if (task.Entity != ev.Old)
                continue;

            _tasks.Add((ev.New, task.Proto), entities);
            _tasks.Remove(task);

            foreach (var entity in entities)
            {
                if (!_htnQuery.TryComp(entity, out var htn)
                    || !_controllableQuery.TryComp(entity, out var control))
                    continue;

                var proto = _prototype.Index(task.Proto);
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
                    value.Add(ev.Target);
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
                    entities.ForEach(entity => TrySetTask(entity, task, ev.Target));
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
        if (!_whitelist.IsWhitelistPass(task.TargetWhitelist, target)
            || _tasks.TryGetValue(new(target, task.ID), out var list) && list.Count >= task.MaxPerformers)
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
    /// Tries to set a new task for an NPC, checking all the required conditions
    /// </summary>
    /// <returns>True, if the task is successfully set</returns>
    public bool TrySetTask(
        Entity<HTNComponent?> npc,
        NpcTaskPrototype proto,
        EntityUid? target = null,
        EntityCoordinates? targetCoords = null)
    {
        if (!Resolve(npc, ref npc.Comp)
            || !_controllableQuery.TryComp(npc, out var control)
            || target != null && !CheckTaskStart(npc.Comp.Blackboard, proto, target.Value))
            return false;

        SetTask(new(npc.Owner, npc.Comp, control), proto, target, targetCoords);
        return true;
    }

    /// <summary>
    /// Creates a new task for the NPC
    /// </summary>
    private void SetTask(
        Entity<HTNComponent, ControllableNpcComponent> entity,
        NpcTaskPrototype proto,
        EntityUid? target = null,
        EntityCoordinates? coords = null)
    {
        var htn = entity.Comp1;
        var control = entity.Comp2;

        if (control.CurrentTask != null)
            FinishTask((entity, control, htn), true);

        var task = (target, proto.ID);

        if (!_tasks.ContainsKey(task))
            _tasks[task] = new();

        _tasks[task].Add(entity);

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

        htn.RootTask = new HTNCompoundTask { Task = proto.Compound };
        _htn.Replan(htn);

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
    public void FinishTask(Entity<ControllableNpcComponent?, HTNComponent?> npc, bool failed)
    {
        if (!Resolve(npc, ref npc.Comp1)
            || !Resolve(npc, ref npc.Comp2)
            || !_prototype.TryIndex(npc.Comp1.CurrentTask, out var proto))
            return;

        var control = npc.Comp1;
        var htn = npc.Comp2;

        if (_tasks.TryGetValue((control.TaskTarget, control.CurrentTask!.Value), out var list))
            list.Remove(npc);

        if (control.TaskTarget != null)
        {
            if (!failed && _passiveTaskQuery.TryComp(control.TaskTarget, out var comp)
                        && comp.Task == control.CurrentTask)
            {
                EntityManager.RemoveComponent(control.TaskTarget.Value, comp);
                var msg = new PassiveNpcTaskRemoveMessage(new() { GetNetEntity(control.TaskTarget.Value) });
                foreach (var user in control.CanControl)
                {
                    RaiseNetworkEvent(msg, user);
                }
            }

            _passiveTasksFails.Remove(control.TaskTarget.Value);
        }

        htn.RootTask = new HTNCompoundTask { Task = proto.OnFinish };
        control.CurrentTask = null;
        control.TaskTarget = null;
        var blackboard = htn.Blackboard;

        // Remove temporary keys from HTNBlackboard
        if (proto.DeleteKeysOnFinish)
        {
            blackboard.Remove<EntityUid>(proto.TargetKey);
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

        RaiseLocalEvent(npc, new NpcTaskFinished(failed, proto));
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
                    && !tileRef.IsSpace())
                    return tileRef;
            }
        }

        return null;
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

    private void SetPassiveTaskTargets(Entity<NpcControlComponent?> user, NpcTaskPrototype proto, List<EntityUid> entities)
    {
        if (!Resolve(user, ref user.Comp) || !user.Comp.Tasks.Contains(proto))
            return;

        var response = new List<NetEntity>();

        foreach (var uid in entities)
        {
            if (_passiveTaskQuery.TryComp(uid, out var task) && task.Task == proto)
                continue;

            // We use the blank NPCBlackboard to test the starting conditions. Nothing can go wrong. Right?
            if (!CheckTaskStart(new NPCBlackboard(), proto, uid))
                continue;

            var comp = EnsureComp<PassiveNpcTaskTargetComponent>(uid);
            comp.Task = proto.ID;
            comp.User = user;

            response.Add(GetNetEntity(uid));
            _passiveTasksFails.Remove(uid);
        }

        if (response.Count == 0)
            return;

        var msg = new PassiveNpcTaskMessage(proto.ID, response);
        RaiseNetworkEvent(msg, user);
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

        var query = EntityQueryEnumerator<TransformComponent, PassiveNpcTaskTargetComponent>();
        while (query.MoveNext(out var uid, out var targetXform, out var comp))
        {
            if (!canControlCache.ContainsKey(comp.User))
                canControlCache.Add(comp.User, CanControl(comp.User, npc));

            if (!canControlCache[comp.User]
                || comp.Task != task
                || _passiveTasksFails.Contains(uid)
                || !CheckTaskStart(npc.Comp.Blackboard, task, uid)
                || !_xformQuery.TryComp(npc, out var xform)
                || !xform.Coordinates.TryDistance(EntityManager, targetXform.Coordinates, out var distance)
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
        return GetPassiveTaskTarget(npc, task) is { } target
               && TrySetTask(npc, task, target);
    }

    /// <summary>
    /// Give the user access to control this NPC
    /// </summary>
    public void AddNpcControl(Entity<NpcControlComponent?> user, EntityUid uid)
    {
        if (!Resolve(user, ref user.Comp))
            return;

        var comp = EnsureComp<ControllableNpcComponent>(uid);
        comp.CanControl.Add(user);
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

            FinishTask(new(uid, comp, htn), false);
        }

        // Checking recently failed tasks
        foreach (var (uid, time) in _fails)
        {
            if (time > _timing.CurTime)
                continue;

            if (_htnQuery.TryComp(uid, out var htn)
                && _controllableQuery.TryComp(uid, out var comp)
                && htn.Plan == null)
            {
                if (_passiveTaskQuery.TryComp(comp.TaskTarget, out var passiveTask) && passiveTask.Task == comp.CurrentTask)
                    _passiveTasksFails.Add(uid);

                FinishTask(new(uid, comp, htn), true);
            }

            _fails.Remove(uid);
        }
    }
}

/// <summary>
/// Raised when an NPC has completed its current task
/// </summary>
[Serializable]
public sealed class NpcTaskFinished(bool failed, ProtoId<NpcTaskPrototype> task) : EntityEventArgs
{
    /// <summary>
    /// Task failed or completed successfully
    /// </summary>
    public bool Failed = failed;

    public ProtoId<NpcTaskPrototype> Task = task;
}
