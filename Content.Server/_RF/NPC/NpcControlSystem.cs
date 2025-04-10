using System.Linq;
using Content.Server.Construction;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared._RF.NPC;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Whitelist;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC;

public sealed class NpcControlSystem : SharedNpcControlSystem
{
    [Dependency] private readonly HTNSystem _htn = default!;
    [Dependency] private readonly NPCSystem _npc = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;

    private const string TargetKey = "Target";
    private const string TargetCoordinatesKey = "TargetCoordinates";

    private readonly Dictionary<EntityUid, HTNCompoundTask> _originCompounds = new();
    private readonly Dictionary<EntityUid, (EntityUid? Target, EntityCoordinates? Coords)> _targets = new();

    private Dictionary<NpcTaskPrototype, List<EntityUid>> _tasks = new();

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<NpcTaskRequest>(OnTaskRequest);
        SubscribeNetworkEvent<NpcTaskResetRequest>(OnTaskResetRequest);
        SubscribeLocalEvent<ConstructionChangeEntityEvent>(OnEntityChange);

        _prototype.PrototypesReloaded += args =>
        {
            if (args.WasModified<NpcTaskPrototype>())
                OnReloadPrototypes();
        };

        OnReloadPrototypes();
    }

    private void OnReloadPrototypes()
    {
        var tasks = new Dictionary<NpcTaskPrototype, List<EntityUid>>();
        foreach (var proto in _prototype.EnumeratePrototypes<NpcTaskPrototype>())
        {
            _tasks.TryGetValue(proto, out var entities);
            tasks.Add(proto, entities ?? new());

            foreach (var precondition in proto.FinishPreconditions)
            {
                precondition.Initialize(EntityManager.EntitySysManager);
            }
        }

        _tasks = tasks;
    }

    /// <summary>
    /// Creates a new task for the NPC and saves the old one
    /// </summary>
    private void SetTask(EntityUid entity, NpcTaskPrototype proto, EntityUid? target, EntityCoordinates? coords)
    {
        if (!_npc.TryGetNpc(entity, out var npc)
            || npc is not HTNComponent htn
            || !TryComp(entity, out ControllableNpcComponent? control))
            return;

        if (htn.Plan != null)
            _htn.ShutdownPlan(htn);

        if (!_originCompounds.ContainsKey(entity))
            _originCompounds[entity] = htn.RootTask;

        _targets[entity] = (target, coords);
        _tasks[proto].Add(entity);
        control.CurrentTask = proto;

        if (target != null)
            npc.Blackboard.SetValue(TargetKey, target);
        if (coords != null)
            npc.Blackboard.SetValue(TargetCoordinatesKey, coords);

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

    private void OnTaskRequest(NpcTaskRequest request)
    {
        var entities = request.Entities.Select(GetEntity).ToList();
        var requester = GetEntity(request.Requester);
        var targets = request.Targets.Select(GetEntity).ToList();
        var targetCoords = GetCoordinates(request.TargetCoordinates);

        EntityUid? target = null;
        NpcTaskPrototype? task = null;

        if (!TryComp(requester, out NpcControlComponent? control))
            return;

        var sorted = control.Tasks
            .Select(t => _prototype.Index(t))
            .OrderBy(x => x.Priority)
            .ToList();

        // Get the first suitable task
        foreach (var entity in targets)
        {
            if (target != null)
                break;

            foreach (var proto in sorted)
            {
                if (!_whitelist.IsWhitelistPass(proto.StartWhitelist, entity))
                    continue;

                task = proto;
                target = entity;
                break;
            }
        }

        // Or take the task without requirements
        task ??= sorted.FirstOrDefault(proto => proto.StartWhitelist == null);
        if (task == null)
            return;

        var previousTargets = new List<TileRef>();

        foreach (var entity in entities)
        {
            if (!TryComp(entity, out ControllableNpcComponent? controllable)
                || !controllable.CanControl.Contains(requester))
                continue;

            if (_tasks[task].Count >= task.MaxNpc)
                break;

            if (target == null)
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

            SetTask(entity, task, target.Value, null);
        }
    }

    private void OnTaskResetRequest(NpcTaskResetRequest request)
    {
        var entity = GetEntity(request.Entity);

        if (!TryComp(entity, out ControllableNpcComponent? controllable)
            || !controllable.CanControl.Contains(GetEntity(request.Requester)))
            return;

        ResetTask(entity);
    }

    // Help construction NPCs keep up-to-date information on the entity to be built
    private void OnEntityChange(ConstructionChangeEntityEvent ev)
    {
        foreach (var (uid, task) in _targets)
        {
            if (task.Target != ev.Old
                || !_npc.TryGetNpc(uid, out var npc))
                continue;

            npc.Blackboard.SetValue(TargetKey, ev.New);
        }
    }

    private void ResetTask(Entity<HTNComponent?, ControllableNpcComponent?> entity)
    {
        if (!Resolve(entity.Owner, ref entity.Comp1)
            || !Resolve(entity.Owner, ref entity.Comp2)
            || !_targets.ContainsKey(entity))
            return;

        if (entity.Comp1.Plan != null)
            _htn.ShutdownPlan(entity.Comp1);

        entity.Comp1.RootTask = _originCompounds[entity];
        entity.Comp2.CurrentTask = null;

        _originCompounds.Remove(entity);
        _targets.Remove(entity);

        foreach (var (_, entities) in _tasks)
        {
            if (entities.Remove(entity))
                break;
        }
        
        RaiseNetworkEvent(new NpcTaskResetMessage { Entity = GetNetEntity(entity) });
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
                    ResetTask(uid);

                comp.TaskFinishAccumulator = comp.TaskFinishCheckRate;
            }

            comp.TaskFinishAccumulator -= frameTime;
        }
    }
}
