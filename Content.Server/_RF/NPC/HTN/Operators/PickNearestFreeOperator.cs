using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Server._RF.World;
using Content.Server.NPC;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._RF.NPC.HTN.Operators;

/// <summary>
/// Finds a random nearest free tile to the given coordinates and overwrites the key with those coordinates
/// </summary>
public sealed partial class PickNearestFreeOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    private RimFortressWorldSystem _world = default!;
    private TurfSystem _turf = default!;
    private MapSystem _map = default!;

    [DataField]
    public string TargetCoordinatesKey = "TargetCoordinates";

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);

        _world = sysManager.GetEntitySystem<RimFortressWorldSystem>();
        _turf = sysManager.GetEntitySystem<TurfSystem>();
        _map = sysManager.GetEntitySystem<MapSystem>();
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard, CancellationToken cancelToken)
    {
        if (!blackboard.TryGetValue(TargetCoordinatesKey, out EntityCoordinates? coordinates, _entityManager))
            return (false, null);

        if (_entityManager.TryGetComponent(coordinates.Value.EntityId, out MapGridComponent? grid)
            && _map.TryGetTileRef(coordinates.Value.EntityId, grid, coordinates.Value, out var tileRef)
            && !_turf.IsTileBlocked(tileRef, CollisionGroup.Impassable ^ CollisionGroup.HighImpassable))
            return (true, null);

        var tiles = _world.GetSpawnTiles(coordinates.Value, 1, 1);

        if (tiles.Count != 1)
            return (false, null);

        var coords = _turf.GetTileCenter(tiles.First());

        return (true, new()
        {
            { TargetCoordinatesKey, coords },
        });
    }
}
