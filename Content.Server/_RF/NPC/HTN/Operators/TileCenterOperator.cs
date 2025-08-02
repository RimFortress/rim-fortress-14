using System.Threading;
using System.Threading.Tasks;
using Content.Server.NPC;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared.Maps;
using Robust.Server.GameObjects;
using Robust.Shared.Map.Components;

namespace Content.Server._RF.NPC.HTN.Operators;

public sealed partial class TileCenterOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    private TransformSystem _xform = default!;
    private MapSystem _map = default!;
    private TurfSystem _turf = default!;

    private EntityQuery<TransformComponent> _xformQuery;
    private EntityQuery<MapGridComponent> _gridQuery;

    [DataField(required: true)]
    public string TargetKey;

    [DataField(required: true)]
    public string ResultKey;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);

        _xform = _entityManager.System<TransformSystem>();
        _map = _entityManager.System<MapSystem>();
        _turf = _entityManager.System<TurfSystem>();

        _xformQuery = _entityManager.GetEntityQuery<TransformComponent>();
        _gridQuery = _entityManager.GetEntityQuery<MapGridComponent>();
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard, CancellationToken cancelToken)
    {
        if (!blackboard.TryGetValue(TargetKey, out EntityUid uid, _entityManager)
            || !_xformQuery.TryComp(uid, out var xform)
            || _xform.GetGrid(uid) is not { } gridUid
            || !_gridQuery.TryComp(gridUid, out var grid)
            || !_map.TryGetTileRef(gridUid, grid, xform.Coordinates, out var tileRef))
            return (false, null);

        return (true, new()
        {
            {ResultKey, _turf.GetTileCenter(tileRef)},
        });
    }
}
