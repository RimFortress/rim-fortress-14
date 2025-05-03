using System.Linq;
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

public sealed class NpcControlSystem : SharedNpcControlSystem
{
    [Dependency] private readonly HTNSystem _htn = default!;
    [Dependency] private readonly NPCSystem _npc = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;

    private EntityQuery<ControllableNpcComponent> _controllableQuery;
    private EntityQuery<PassiveNpcTaskTargetComponent> _passiveTaskQuery;
    private EntityQuery<TransformComponent> _xformQuery;

    private readonly Dictionary<EntityUid, List<EntityUid>> _selected = new();
    private readonly Dictionary<(EntityUid? Entity, ProtoId<NpcTaskPrototype> Proto), List<EntityUid>> _tasks = new();
    private readonly Dictionary<TimeSpan, EntityUid> _fails = new();
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
                SetTask(entity, task, target, null);
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
                SetTask(entity, task, null, tileCoords);
                continue;
            }

            if (GetNeighborTile(previousTargets) is not { } tile)
                continue;

            previousTargets.Add(tile);

            var tileCenter = _turf.GetTileCenter(tile);
            SetTask(entity, task, null, tileCenter);
        }
    }

    private void OnPassiveTaskRequest(PassiveNpcTaskRequest request)
    {
        if (!_prototype.TryIndex<NpcTaskPrototype>(request.TaskId, out var task))
            return;

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
        var entities = EntityQueryEnumerator<ControllableNpcComponent, HTNComponent>();
        while (entities.MoveNext(out var uid, out var control, out var htn))
        {
            if (!_prototype.TryIndex(control.CurrentTask, out var proto)
                || !htn.Blackboard.TryGetValue(proto.TargetKey, out EntityUid? target, EntityManager)
                || target != ev.Old)
                continue;

            if (_tasks.TryGetValue((ev.Old, control.CurrentTask!.Value), out var tasks))
            {
                _tasks[(ev.New, control.CurrentTask!.Value)] = tasks;
                _tasks.Remove((ev.Old, control.CurrentTask!.Value));
            }

            htn.Blackboard.SetValue(proto.TargetKey, ev.New);
            foreach (var entity in control.CanControl)
            {
                RaiseNetworkEvent(NpcTaskInfo(uid, proto, target), entity);
            }
        }
    }

    private void OnGetVerbs(GetVerbsEvent<Verb> ev)
    {
        if (!TryComp(ev.User, out NpcControlComponent? control)
            || !_selected.TryGetValue(ev.User, out var selected))
            return;

        var tasks = new Dictionary<NpcTaskPrototype, List<EntityUid>>();
        foreach (var entity in selected)
        {
            if (FindSatisfiedTasks(entity, ev.Target, control.Tasks.Select(_prototype.Index).ToList()) is not { } suitable)
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
                    entities.ForEach(entity => SetTask(entity, task, ev.Target, null));
                    _selected.Remove(ev.User);
                },
            });
        }
    }

    private void OnPlanningFailed(HtnPlanningFailed ev)
    {
        if (!_controllableQuery.TryComp(ev.Entity, out var control)
            || !_prototype.TryIndex(control.CurrentTask, out var proto))
            return;

        // If the task planning fails, we start the countdown,
        // at the end of which we check again whether the task planning succeeded or not
        _fails.Add(_timing.CurTime + proto.FailAwaitTime, ev.Entity);
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
        List<NpcTaskPrototype>? suitable = null;

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
                || !_whitelist.IsWhitelistPass(proto.TargetWhitelist, target.Value)
                || uid == target && !proto.SelfPerform
                || _tasks.TryGetValue(new(target, proto.ID), out var list)
                && list.Count >= proto.MaxPerformers)
                continue;

            if (!CheckTaskStart(npc.Blackboard, proto, target.Value))
                continue;

            suitable ??= new();
            suitable.Add(proto);
        }

        return suitable ?? zeroTasks;
    }

    private bool CheckTaskStart(NPCBlackboard blackboard, NpcTaskPrototype task, EntityUid target)
    {
        // Set a temporary variable in NPCBlackboard to check conditions
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
    /// Creates a new task for the NPC
    /// </summary>
    private void SetTask(EntityUid entity, NpcTaskPrototype proto, EntityUid? target, EntityCoordinates? coords)
    {
        if (!_npc.TryGetNpc(entity, out var npc)
            || npc is not HTNComponent htn
            || !_controllableQuery.TryComp(entity, out var control))
            return;

        if (control.CurrentTask != null)
            FinishTask((entity, control, htn));

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
            npc.Blackboard.SetValue(proto.TargetKey, target);
        if (coords != null)
            npc.Blackboard.SetValue(proto.TargetCoordinatesKey, coords);

        htn.RootTask = new HTNCompoundTask { Task = proto.Compound };
        _htn.Replan(htn);

        foreach (var uid in control.CanControl)
        {
            RaiseNetworkEvent(NpcTaskInfo(entity, proto, target, coords), uid);
        }
    }

    private void FinishTask(Entity<ControllableNpcComponent?, HTNComponent?> entity)
    {
        if (!Resolve(entity, ref entity.Comp1)
            || !Resolve(entity, ref entity.Comp2)
            || !_prototype.TryIndex(entity.Comp1.CurrentTask, out var proto))
            return;

        if (_tasks.TryGetValue((entity.Comp1.TaskTarget, entity.Comp1.CurrentTask!.Value), out var list))
            list.Remove(entity);

        if (entity.Comp1.TaskTarget != null)
            _passiveTasksFails.Remove(entity.Comp1.TaskTarget.Value);

        entity.Comp2.RootTask = new HTNCompoundTask { Task = proto.OnFinish };
        entity.Comp1.CurrentTask = null;
        entity.Comp1.TaskTarget = null;
        var blackboard = entity.Comp2.Blackboard;

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

        foreach (var uid in entity.Comp1.CanControl)
        {
            RaiseNetworkEvent(new NpcTaskFinishMessage(proto.ID, GetNetEntity(entity)), uid);
        }
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
            if (!_whitelist.IsWhitelistPass(proto.TargetWhitelist, uid)
                || !CheckTaskStart(new NPCBlackboard(), proto, uid))
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

    public EntityUid? GetPassiveTaskTarget(Entity<HTNComponent?> entity, NpcTaskPrototype task)
    {
        if (!Resolve(entity, ref entity.Comp))
            return null;

        EntityUid? target = null;
        var minDist = (float) int.MaxValue;
        var canControlCache = new Dictionary<EntityUid, bool>();

        var query = EntityQueryEnumerator<TransformComponent, PassiveNpcTaskTargetComponent>();
        while (query.MoveNext(out var uid, out var targetXform, out var comp))
        {
            if (!canControlCache.ContainsKey(comp.User))
                canControlCache.Add(comp.User, CanControl(comp.User, entity));

            if (!canControlCache[comp.User]
                || comp.Task != task
                || _passiveTasksFails.Contains(uid)
                || !CheckTaskStart(entity.Comp.Blackboard, task, uid)
                || !_xformQuery.TryComp(entity, out var xform)
                || !xform.Coordinates.TryDistance(EntityManager, targetXform.Coordinates, out var distance)
                || distance >= minDist)
                continue;

            minDist = distance;
            target = uid;
        }

        return target;
    }

    private NpcTaskInfoMessage NpcTaskInfo(EntityUid entity,
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

            comp.TaskFinishAccumulator -= frameTime;

            if (comp.TaskFinishAccumulator > 0)
                continue;

            var needFinish = proto.FinishPreconditions.Count != 0;
            comp.TaskFinishAccumulator = comp.TaskFinishCheckRate;

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

        // Checking recently failed assignments
        foreach (var (time, uid) in _fails)
        {
            if (time > _timing.CurTime)
                continue;

            if (TryComp(uid, out HTNComponent? htn) && _controllableQuery.TryComp(uid, out var comp) && htn.Plan == null)
            {
                if (_passiveTaskQuery.TryComp(comp.TaskTarget, out var passiveTask) && passiveTask.Task == comp.CurrentTask)
                    _passiveTasksFails.Add(uid);

                FinishTask(new(uid, comp, htn));
            }

            _fails.Remove(time);
        }
    }
}
