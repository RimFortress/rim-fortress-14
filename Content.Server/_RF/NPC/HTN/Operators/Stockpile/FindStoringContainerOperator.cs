using System.Threading;
using System.Threading.Tasks;
using Content.Server._RF.Stockpile;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server.NPC.Pathfinding;
using Content.Server.Storage.Components;
using Content.Shared._RF.Stockpile;
using Robust.Server.GameObjects;
using Robust.Shared.Map.Components;

namespace Content.Server._RF.NPC.HTN.Operators.Stockpile;

public sealed partial class FindStoringContainerOperator : HTNOperator, IHtnConditionalShutdown
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    private PathfindingSystem _pathfind = default!;
    private SharedTransformSystem _transform = default!;
    private StockpileSystem _stockpile = default!;
    private EntityLookupSystem _lookup = default!;
    private MapSystem _map = default!;

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
    /// The key of the entity for which you want to find a container in the stockpile
    /// </summary>
    [DataField(required: true)]
    public string TargetKey;

    /// <summary>
    /// Key in which the found container place will be stored
    /// </summary>
    [DataField(required: true)]
    public string ResultKey;

    /// <summary>
    /// How close we should try to build a path to the container
    /// </summary>
    [DataField]
    public string RangeKey = "MovementRange";

    /// <summary>
    /// If true, only stockpiles supplied from the stockpile where the target entity is located will be selected for stockpiling
    /// </summary>
    [DataField]
    public bool SupplyingOnly;

    /// <summary>
    /// A key from <see cref="NPCBlackboard"/> that can be set to SupplyingOnly
    /// </summary>
    [DataField]
    public string SupplyingOnlyKey = "SupplyingOnly";

    /// <summary>
    /// A key containing a stockpile that is the start of a supply chain.
    /// </summary>
    [DataField]
    public string SupplyingStartStockKey = "SupplyingStartStock";

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _pathfind = sysManager.GetEntitySystem<PathfindingSystem>();
        _transform = sysManager.GetEntitySystem<SharedTransformSystem>();
        _stockpile = sysManager.GetEntitySystem<StockpileSystem>();
        _lookup = sysManager.GetEntitySystem<EntityLookupSystem>();
        _map = sysManager.GetEntitySystem<MapSystem>();
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard, CancellationToken cancelToken)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        var range = blackboard.GetValueOrDefault<float>(RangeKey, _entManager);

        if (!blackboard.TryGetValue(TargetKey, out EntityUid uid, _entManager))
            return (false, null);

        List<Stock> stockpiles;

        if (blackboard.TryGetValue(SupplyingOnlyKey, out bool only, _entManager) && only || SupplyingOnly)
        {
            if (!blackboard.TryGetValue(SupplyingStartStockKey, out Stock? stock, _entManager))
                return (false, null);

            stockpiles = _stockpile.FindLastSupplied(uid, stock);
        }
        else
            stockpiles = _stockpile.AllStockpiles();

        foreach (var stock in stockpiles)
        {
            var result = await _stockpile.GetStoringTilePath(owner,
                uid,
                stock,
                range,
                cancelToken,
                _pathfind.GetFlags(blackboard),
                true);

            if (result == null
                || _transform.GetGrid(result.Value.Coords) is not { } gridUid
                || !_entManager.TryGetComponent(gridUid, out MapGridComponent? grid)
                || !_map.TryGetTileRef(gridUid, grid, result.Value.Coords, out var tileRef))
                continue;

            var bounds = _lookup.GetLocalBounds(tileRef, grid.TileSize).Enlarged(0.5f);
            var entities = _lookup.GetEntitiesIntersecting(gridUid, bounds);

            foreach (var entity in entities)
            {
                if (!_entManager.HasComponent<EntityStorageComponent>(entity))
                    continue;

                return (true, new()
                {
                    { PathfindKey, result.Value.Path },
                    { ResultKey, entity },
                });
            }
        }

        return (false, null);
    }

    public void ConditionalShutdown(NPCBlackboard blackboard)
    {
        blackboard.Remove<PathResultEvent>(PathfindKey);
    }
}
