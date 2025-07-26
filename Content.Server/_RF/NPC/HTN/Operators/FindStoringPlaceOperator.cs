using System.Threading;
using System.Threading.Tasks;
using Content.Server._RF.Stockpile;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server.NPC.Pathfinding;
using Content.Shared._RF.NPC;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._RF.NPC.HTN.Operators;

public sealed partial class FindStoringPlaceOperator : HTNOperator, IHtnConditionalShutdown
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    private PathfindingSystem _pathfind = default!;
    private SharedTransformSystem _transform = default!;
    private MapSystem _map = default!;
    private TurfSystem _turf = default!;
    private StockpileSystem _stockpile = default!;

    /// <summary>
    /// When to shut the task down
    /// </summary>
    [DataField]
    public HTNPlanState ShutdownState { get; private set; } = HTNPlanState.TaskFinished;

    /// <summary>
    /// Where the pathfinding result will be stored
    /// </summary>
    [DataField]
    public string PathfindKey = NPCBlackboard.PathfindKey;

    /// <summary>
    /// The key of the entity for which you want to find a place in the stockpile
    /// </summary>
    [DataField(required: true)]
    public string TargetKey;

    /// <summary>
    /// Key in which the coordinates of the found storage place will be stored
    /// </summary>
    [DataField]
    public string CoordinatesKey = "TargetCoordinates";

    /// <summary>
    /// How close we should try to build a path to the stockpile place
    /// </summary>
    [DataField]
    public string RangeKey = "MovementRange";

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _pathfind = sysManager.GetEntitySystem<PathfindingSystem>();
        _transform = sysManager.GetEntitySystem<SharedTransformSystem>();
        _map = sysManager.GetEntitySystem<MapSystem>();
        _turf = sysManager.GetEntitySystem<TurfSystem>();
        _stockpile = sysManager.GetEntitySystem<StockpileSystem>();
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard, CancellationToken cancelToken)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        var range = blackboard.GetValueOrDefault<float>(RangeKey, _entManager);

        if (_transform.GetGrid(owner) is not { } gridUid
            || !_entManager.TryGetComponent(gridUid, out MapGridComponent? grid)
            || !blackboard.TryGetValue(TargetKey, out EntityUid? uid, _entManager)
            || !_entManager.TryGetComponent(uid, out MetaDataComponent? meta)
            || meta.EntityPrototype is not { } proto
            || !_entManager.TryGetComponent(uid, out OwnedComponent? owned)
            || !_entManager.TryGetComponent(uid, out TransformComponent? xform))
            return (false, null);

        var freePlaces = new List<(EntityCoordinates Coord, float Dist)>();

        foreach (var stock in _stockpile.AllStockpiles())
        {
            if (!owned.Owners.Contains(stock.Owner)
                || stock.GridUid != gridUid
                || !stock.CanInsert(proto))
                continue;

            foreach (var tile in stock.FreeTiles)
            {
                var tileRef = _map.GetTileRef(new(gridUid, grid), tile);

                if (_turf.IsTileBlocked(tileRef, CollisionGroup.Impassable ^ CollisionGroup.HighImpassable))
                    continue;

                var center = _turf.GetTileCenter(tileRef);

                if (!xform.Coordinates.TryDistance(_entManager, _transform, center, out var distance))
                    continue;

                freePlaces.Add((center, distance));
            }
        }

        freePlaces.Sort((x, y) => x.Dist.CompareTo(y.Dist));

        foreach (var (coord, _) in freePlaces)
        {
            var path = await _pathfind.GetPath(
                blackboard.GetValue<EntityUid>(NPCBlackboard.Owner),
                xform.Coordinates,
                coord,
                range,
                cancelToken,
                _pathfind.GetFlags(blackboard));

            if (path.Result != PathResult.Path)
                continue;

            return (true, new()
            {
                { PathfindKey, path },
                { CoordinatesKey, coord },
            });
        }

        return (false, null);
    }

    public void ConditionalShutdown(NPCBlackboard blackboard)
    {
        blackboard.Remove<PathResultEvent>(PathfindKey);
    }
}
