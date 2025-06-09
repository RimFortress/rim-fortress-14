using System.Threading;
using System.Threading.Tasks;
using Content.Server.NPC.Pathfinding;
using Content.Shared._RF.Stockpile;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.Stockpile;

public sealed class StockpileSystem : SharedStockpileSystem
{
    [Dependency] private readonly PathfindingSystem _pathfinding  = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<StockpileCreated>(OnStockpileCreated);
    }

    private void OnStockpileCreated(StockpileCreated ev)
    {
        CreateStockpile(ev.Tiles, GetEntity(ev.GridUid));
    }

    /// <summary>
    /// Returns the closest tile available for storing the item
    /// </summary>
    /// <param name="uid">An entity that attempts to stockpile an object</param>
    /// <param name="protoId">Prototype entity for stockpiling</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<TileRef?> GetAccessibleTile(EntityUid uid, EntProtoId protoId, CancellationToken cancellationToken)
    {
        if (Xform.GetGrid(uid) is not { } gridUid || !TryComp(gridUid, out MapGridComponent? grid))
            return null;

        var freeTiles = new List<TileRef>();
        var ent = new Entity<MapGridComponent>(gridUid, grid);

        foreach (var stock in Stockpiles)
        {
            if (stock.GridUid != gridUid || !stock.CanInsert(protoId))
                continue;

            foreach (var tile in stock.FreeTiles)
            {
                freeTiles.Add(Map.GetTileRef(ent, tile));
            }
        }

        freeTiles.Sort((x1, x2) =>
        {
            var coords1 = _turf.GetTileCenter(x1);
            var coords2 = _turf.GetTileCenter(x2);

            return coords1.TryDistance(EntityManager, coords2, out var distance) ? (int) distance : int.MaxValue;
        });

        foreach (var tile in freeTiles)
        {
            var path = await _pathfinding.GetPath(uid,
                Transform(uid).Coordinates,
                _turf.GetTileCenter(tile),
                0.1f,
                cancellationToken);

            if (path.Result != PathResult.Path)
                continue;

            return tile;
        }

        return null;
    }
}
