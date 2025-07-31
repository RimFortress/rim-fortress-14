using Content.Server.NPC;
using Content.Shared.Maps;
using Robust.Server.GameObjects;
using Robust.Shared.Map.Components;

namespace Content.Server._RF.NPC.Queries.Filters;

/// <summary>
/// Filters entities that are within a specified distance from the center of the tile
/// </summary>
public sealed partial class TileOffsetFilter : RfUtilityQueryFilter
{
    private TransformSystem _xform = default!;
    private MapSystem _map = default!;
    private TurfSystem _turf = default!;

    private EntityQuery<TransformComponent> _xformQuery;
    private EntityQuery<MapGridComponent> _gridQuery;

    /// <summary>
    /// Entities that are farther away from the center of the tile will be filtered out
    /// </summary>
    [DataField]
    public float? MoreThan;

    /// <summary>
    /// Entities that are less distant from the center of the tile will be filtered out
    /// </summary>
    [DataField]
    public float? LessThan;

    public override void Initialize(IEntityManager entManager)
    {
        base.Initialize(entManager);
        _xform = entManager.System<TransformSystem>();
        _map = entManager.System<MapSystem>();
        _turf = entManager.System<TurfSystem>();

        _xformQuery = entManager.GetEntityQuery<TransformComponent>();
        _gridQuery = entManager.GetEntityQuery<MapGridComponent>();
    }

    public override bool Filter(EntityUid uid, NPCBlackboard blackboard)
    {
        if (!_xformQuery.TryComp(uid, out var xform)
            || _xform.GetGrid(uid) is not { } gridUid
            || !_gridQuery.TryComp(gridUid, out var grid)
            || !_map.TryGetTileRef(gridUid, grid, xform.Coordinates, out var tileRef)
            || !xform.Coordinates.TryDistance(EntityManager, _xform, _turf.GetTileCenter(tileRef), out var dist))
            return false;

        return dist >  MoreThan || dist < LessThan;
    }
}
