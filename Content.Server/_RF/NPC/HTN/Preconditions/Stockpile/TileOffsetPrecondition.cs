using Content.Server.NPC;
using Content.Shared.Maps;
using Robust.Server.GameObjects;
using Robust.Shared.Map.Components;

namespace Content.Server._RF.NPC.HTN.Preconditions.Stockpile;

/// <summary>
/// Checks the offset of the target entity from the center of the tile on which it is located
/// </summary>
public sealed partial class TileOffsetPrecondition : InvertiblePrecondition
{
    private TransformSystem _xform = default!;
    private MapSystem _map = default!;
    private TurfSystem _turf = default!;

    private EntityQuery<TransformComponent> _xformQuery;
    private EntityQuery<MapGridComponent> _gridQuery;

    [DataField]
    public float? MoreThan;

    [DataField]
    public float? LessThan;

    [DataField(required: true)]
    public string TargetKey;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _xform = sysManager.GetEntitySystem<TransformSystem>();
        _map = sysManager.GetEntitySystem<MapSystem>();
        _turf = sysManager.GetEntitySystem<TurfSystem>();

        _xformQuery = EntityManager.GetEntityQuery<TransformComponent>();
        _gridQuery = EntityManager.GetEntityQuery<MapGridComponent>();
    }

    public override bool IsMetInvertible(NPCBlackboard blackboard)
    {
        if (!blackboard.TryGetValue(TargetKey, out EntityUid uid, EntityManager)
            || !_xformQuery.TryComp(uid, out var xform)
            || _xform.GetGrid(uid) is not { } gridUid
            || !_gridQuery.TryComp(gridUid, out var grid)
            || !_map.TryGetTileRef(gridUid, grid, xform.Coordinates, out var tileRef)
            || !xform.Coordinates.TryDistance(EntityManager, _xform, _turf.GetTileCenter(tileRef), out var dist))
            return false;

        return dist >  MoreThan || dist < LessThan;
    }
}
