using System.Linq;
using Content.Server.Construction;
using Content.Server.Construction.Components;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared._RF.NPC;
using Content.Shared.Item;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._RF.NPC;

public sealed class NpcControlSystem : SharedNpcControlSystem
{
    [Dependency] private readonly HTNSystem _htn = default!;
    [Dependency] private readonly NPCSystem _npc = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;

    private const string TargetKey = "Target";
    private const string TargetCoordinatesKey = "TargetCoordinates";

    private readonly Dictionary<EntityUid, HTNCompoundTask> _originCompounds = new();
    private readonly Dictionary<EntityUid, NpcTask> _tasks = new();

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<NpcTaskRequest>(OnTaskRequest);
        SubscribeNetworkEvent<NpcTaskResetRequest>(OnTaskResetRequest);
        SubscribeLocalEvent<ConstructionChangeEntityEvent>(OnEntityChange);
    }

    /// <summary>
    /// Creates a new task for the NPC and saves the old one
    /// </summary>
    private void SetTask(EntityUid entity, NpcTask task, string compoundTask)
    {
        if (!_npc.TryGetNpc(entity, out var npc)
            || npc is not HTNComponent htn)
            return;

        if (htn.Plan != null)
            _htn.ShutdownPlan(htn);

        if (!_originCompounds.ContainsKey(entity))
            _originCompounds[entity] = htn.RootTask;

        _tasks[entity] = task;
        htn.RootTask = new HTNCompoundTask { Task = compoundTask };
        _htn.Replan(htn);

        var msg = new NpcTaskInfoMessage
        {
            Entity = GetNetEntity(entity),
            TaskType = task.Type,
            Target = GetNetEntity(task.Target),
            TargetCoordinates = GetNetCoordinates(task.Coordinates),
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
        var type = NpcTaskType.Move;

        foreach (var entity in targets)
        {
            if (TryComp(entity, out HTNComponent? _))
            {
                type = NpcTaskType.Attack;
                target = entity;
                break;
            }

            if (TryComp(entity, out ItemComponent? _))
            {
                type = NpcTaskType.PickUp;
                target = entity;
                break;
            }

            if (TryComp(entity, out ConstructionComponent? _))
            {
                type = NpcTaskType.Build;
                target = entity;
                break;
            }
        }

        var previousTargets = new List<TileRef>();

        foreach (var entity in entities)
        {
            if (!_npc.TryGetNpc(entity, out var npc)
                || !TryComp(entity, out ControllableNpcComponent? controllable)
                || !controllable.CanControl.Contains(requester)
                || !controllable.Compounds.TryGetValue(type, out var compoundTask))
                continue;

            // Gives all selected entities a task to go to a given tile
            if (type == NpcTaskType.Move)
            {
                if (previousTargets.Count == 0)
                {
                    if (!TryComp(targetCoords.EntityId, out MapGridComponent? grid)
                        || !_map.TryGetTileRef(targetCoords.EntityId, grid, targetCoords, out var tileRef)
                        || _turf.IsTileBlocked(tileRef, CollisionGroup.Impassable ^ CollisionGroup.HighImpassable))
                        break;

                    previousTargets.Add(tileRef);

                    var tileCoords = _turf.GetTileCenter(tileRef);
                    npc.Blackboard.SetValue(TargetCoordinatesKey, tileCoords);
                    SetTask(entity, new NpcTask(NpcTaskType.Move, tileCoords.EntityId, tileCoords), compoundTask);
                    continue;
                }

                if (GetNeighborTile(previousTargets) is not { } tile)
                    continue;

                previousTargets.Add(tile);
                var tileCenter = _turf.GetTileCenter(tile);

                var task = new NpcTask(NpcTaskType.Move, targetCoords.EntityId, tileCenter);
                npc.Blackboard.SetValue(TargetCoordinatesKey, tileCenter);

                SetTask(entity, task, compoundTask);
                continue;
            }

            if (target is not { } uid)
                continue;

            var coords = Transform(uid).Coordinates;

            npc.Blackboard.SetValue(TargetKey, target);
            npc.Blackboard.SetValue(TargetCoordinatesKey, coords);

            SetTask(entity, new NpcTask(type, uid, coords), compoundTask);
        }
    }

    private void OnTaskResetRequest(NpcTaskResetRequest request)
    {
        var entity = GetEntity(request.Entity);
        var requester = GetEntity(request.Requester);

        if (!TryComp(entity, out ControllableNpcComponent? controllable)
            || !controllable.CanControl.Contains(requester)
            || !_tasks.ContainsKey(entity)
            || !_npc.TryGetNpc(entity, out var npc)
            || npc is not HTNComponent htn)
            return;

        if (htn.Plan != null)
            _htn.ShutdownPlan(htn);

        htn.RootTask = _originCompounds[entity];

        _originCompounds.Remove(entity);
        _tasks.Remove(entity);
    }

    // Help construction NPCs keep up-to-date information on the entity to be built
    private void OnEntityChange(ConstructionChangeEntityEvent ev)
    {
        foreach (var (uid, task) in _tasks)
        {
            if (task.Type != NpcTaskType.Build
                || task.Target != ev.Old
                || !_npc.TryGetNpc(uid, out var npc))
                continue;

            npc.Blackboard.SetValue(TargetKey, ev.New);
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
}
