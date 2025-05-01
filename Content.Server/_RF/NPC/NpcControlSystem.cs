using System.Linq;
using Content.Server.Construction;
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
    private readonly Dictionary<EntityUid, List<EntityUid>> _selected = new();
    private readonly Dictionary<(EntityUid? Entity, ProtoId<NpcTaskPrototype> Proto), List<EntityUid>> _tasks = new();
    private readonly Dictionary<TimeSpan, EntityUid> _fails = new();

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<NpcTaskRequest>(OnTaskRequest);
        SubscribeLocalEvent<ConstructionChangeEntityEvent>(OnEntityChange);
        SubscribeLocalEvent<GetVerbsEvent<Verb>>(OnGetVerbs);
        SubscribeLocalEvent<HtnPlanningFailed>(OnPlanningFailed);

        _prototype.PrototypesReloaded += args =>
        {
            if (args.WasModified<NpcTaskPrototype>())
                ReloadPrototypes();
        };

        _controllableQuery = GetEntityQuery<ControllableNpcComponent>();

        ReloadPrototypes();
    }

    #region Event Handle

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

        if (verb)
        {
            RaiseNetworkEvent(new NpcTasksContextMenuMessage());
            return;
        }

        _selected.Remove(requester);

        foreach (var (entity, tasks) in satisfiedTasks)
        {
            var task = tasks[0];

            if (task.TargetWhitelist == null)
            {
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
                continue;
            }

            SetTask(entity, task, target, null);
        }
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

            var msg = new NpcTaskInfoMessage
            {
                Entity = GetNetEntity(uid),
                Color = proto.OverlayColor,
                Target = GetNetEntity(target),
                TargetCoordinates = GetNetCoordinates(Transform(uid).Coordinates),
            };

            RaiseNetworkEvent(msg);
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

        _fails.Add(_timing.CurTime + proto.FailAwaitTime, ev.Entity);
    }

    #endregion

    /// <summary>
    /// Finds the suitable tasks for the target from the task list
    /// </summary>
    private List<NpcTaskPrototype>? FindSatisfiedTasks(EntityUid uid, EntityUid? target, List<NpcTaskPrototype> tasks)
    {
        var suitable = new List<NpcTaskPrototype>();

        if (!_npc.TryGetNpc(uid, out var npc))
            return null;

        foreach (var proto in tasks)
        {
            if (target == null)
            {
                if (proto.TargetWhitelist == null)
                    suitable.Add(proto);

                continue;
            }

            if (!_whitelist.IsWhitelistPass(proto.TargetWhitelist, target.Value)
                || uid == target && !proto.SelfPerform
                || _tasks.TryGetValue(new(target, proto.ID), out var list)
                && list.Count >= proto.MaxPerformers)
                continue;

            var valid = true;

            // Set a temporary variable in NPCBlackboard to check conditions
            npc.Blackboard.SetValue(proto.TargetKey, target);

            // Checking the fulfillment of additional starting conditions
            foreach (var condition in proto.StartPreconditions)
            {
                if (condition.IsMet(npc.Blackboard))
                    continue;

                valid = false;
                break;
            }

            npc.Blackboard.Remove<EntityUid>(proto.TargetKey);

            if (!valid)
                continue;

            suitable.Add(proto);
        }

        return suitable.Count == 0 ? null : suitable;
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

        var msg = new NpcTaskInfoMessage
        {
            Entity = GetNetEntity(entity),
            Color = proto.OverlayColor,
            Target = GetNetEntity(target),
            TargetCoordinates = GetNetCoordinates(coords),
        };

        RaiseNetworkEvent(msg);
    }

    private void FinishTask(Entity<ControllableNpcComponent?, HTNComponent?> entity)
    {
        if (!Resolve(entity, ref entity.Comp1)
            || !Resolve(entity, ref entity.Comp2)
            || !_prototype.TryIndex(entity.Comp1.CurrentTask, out var proto))
            return;

        if (_tasks.TryGetValue((entity.Comp1.TaskTarget, entity.Comp1.CurrentTask!.Value), out var list))
            list.Remove(entity);

        entity.Comp2.RootTask = new HTNCompoundTask { Task = proto.OnFinish };
        entity.Comp1.CurrentTask = null;
        entity.Comp1.TaskTarget = null;

        if (proto.DeleteKeysOnFinish)
        {
            entity.Comp2.Blackboard.Remove<EntityUid>(proto.TargetKey);
            entity.Comp2.Blackboard.Remove<EntityCoordinates>(proto.TargetCoordinatesKey);
        }

        foreach (var key in proto.TempKeys)
        {
            if (entity.Comp2.Blackboard.ContainsKey(key))
                entity.Comp2.Blackboard.Remove(key);
        }

        RaiseNetworkEvent(new NpcTaskInfoMessage
        {
            Entity = GetNetEntity(entity),
            Color = proto.OverlayColor,
        });
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

    public bool CanControl(EntityUid requester, EntityUid entity)
    {
        if (!_controllableQuery.TryComp(entity, out var control)
            || !TryComp(entity, out MobStateComponent? mobState)
            || mobState.CurrentState != MobState.Alive
            || !control.CanControl.Contains(requester))
            return false;

        return true;
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<ControllableNpcComponent, HTNComponent>();
        while (query.MoveNext(out var uid, out var comp, out var htn))
        {
            if (comp.CurrentTask == null)
                continue;

            if (comp.TaskFinishAccumulator < 0)
            {
                var proto = _prototype.Index<NpcTaskPrototype>(comp.CurrentTask);
                var needFinish = proto.FinishPreconditions.Count != 0;

                foreach (var precondition in proto.FinishPreconditions)
                {
                    if (precondition.IsMet(htn.Blackboard))
                        continue;

                    needFinish = false;
                    break;
                }

                if (needFinish)
                {
                    if (htn.Plan != null)
                    {
                        _htn.ShutdownTask(htn.Plan.CurrentOperator, htn.Blackboard, HTNOperatorStatus.Failed);
                        _htn.ShutdownPlan(htn);
                        htn.Plan = null;
                    }

                    FinishTask(new(uid, comp, htn));
                }

                comp.TaskFinishAccumulator = comp.TaskFinishCheckRate;
            }

            comp.TaskFinishAccumulator -= frameTime;
        }

        foreach (var (time, uid) in _fails)
        {
            if (time > _timing.CurTime)
                continue;

            if (TryComp(uid, out HTNComponent? htn) && htn.Plan == null)
                FinishTask(new(uid, null, htn));

            _fails.Remove(time);
        }
    }
}
