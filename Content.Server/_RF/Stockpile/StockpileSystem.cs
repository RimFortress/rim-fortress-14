using System.Threading;
using Content.Shared._RF.Stockpile;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.Stockpile;

public sealed class StockpileSystem : SharedStockpileSystem
{
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
    /// Returns available tiles for storing the item
    /// </summary>
    /// <param name="uid">An entity that attempts to stockpile an object</param>
    /// <param name="protoId">Prototype entity for stockpiling</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public List<TileRef> GetFreeTile(EntityUid uid, EntProtoId protoId, CancellationToken cancellationToken)
    {
        if (Xform.GetGrid(uid) is not { } gridUid || !TryComp(gridUid, out MapGridComponent? grid))
            return new();

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

        return freeTiles;
    }
}
