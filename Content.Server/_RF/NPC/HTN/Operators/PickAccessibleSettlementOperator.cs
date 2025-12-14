using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Server._RF.World;
using Content.Server.NPC;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server.NPC.Pathfinding;
using Content.Shared.Maps;
using Robust.Shared.Map;

namespace Content.Server._RF.NPC.HTN.Operators;

/// <summary>
/// Finds random coordinates for a settlement relative to the current position
/// </summary>
public sealed partial class PickAccessibleSettlementOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entity = default!;

    private RimFortressWorldSystem _world = default!;
    private PathfindingSystem _pathfinding = default!;
    private TurfSystem _turf = default!;

    /// <summary>
    /// Where the pathfinding result will be stored (if applicable). This gets removed after execution.
    /// </summary>
    [DataField]
    public string PathfindingKey = NPCBlackboard.PathfindKey;

    [DataField]
    public string RangeKey = NPCBlackboard.MoveToCloseRange;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _world = sysManager.GetEntitySystem<RimFortressWorldSystem>();
        _pathfinding = sysManager.GetEntitySystem<PathfindingSystem>();
        _turf = sysManager.GetEntitySystem<TurfSystem>();
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard, CancellationToken cancelToken)
    {
        if (!blackboard.TryGetValue<EntityCoordinates>(NPCBlackboard.OwnerCoordinates, out var coords, _entity)
            || !blackboard.TryGetValue<float>(RangeKey, out var range, _entity))
            return (false, null);

        var tiles = _world.GetSpawnTiles(coords, 1);

        if (tiles.Count <= 0)
            return (false, null);

        var targetCoords = _turf.GetTileCenter(tiles.First());

        var path = await _pathfinding.GetPath(
            blackboard.GetValue<EntityUid>(NPCBlackboard.Owner),
            coords,
            targetCoords,
            range,
            cancelToken,
            flags: _pathfinding.GetFlags(blackboard));

        if (path.Result != PathResult.Path)
            return (false, null);

        return (true, new()
        {
            { PathfindingKey, path },
        });
    }
}
