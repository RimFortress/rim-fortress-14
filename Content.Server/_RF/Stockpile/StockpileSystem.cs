using Content.Server.Storage.Components;
using Content.Shared._RF.Stockpile;
using Content.Shared.Physics;
using Robust.Shared.Map.Components;

namespace Content.Server._RF.Stockpile;

public sealed class StockpileSystem : SharedStockpileSystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StockpileCategoryComponent, MoveEvent>(OnMove);
    }

    private void OnMove(EntityUid uid, StockpileCategoryComponent component, MoveEvent args)
    {
        if (Xform.GetGrid(uid) is not { } gridUid
            || !TryComp(gridUid, out MapGridComponent? grid)
            || !Map.TryGetTileRef(gridUid, grid, Transform(uid).Coordinates, out var tileRef)
            || Turf.IsTileBlocked(tileRef, CollisionGroup.Impassable ^ CollisionGroup.HighImpassable))
            return;

        TryGetContainingStock(uid, out var oldStock);
        TryGetStock(tileRef, out var newStock);

        if (newStock != null
            && newStock == oldStock
            && oldStock.TryGetContainingTile(uid, out var oldTile)
            && oldTile != tileRef.GridIndices)
        {
            if (!newStock.TryUpdateEntityTile(uid, tileRef.GridIndices))
                DetachEntity(uid, newStock);
        }

        if (oldStock != null && newStock == null)
            DetachEntity(uid, oldStock);

        if (newStock != null && oldStock != newStock)
        {
            if (oldStock != null)
                DetachEntity(uid, oldStock);

            if (TryComp(uid, out EntityStorageComponent? storage))
            {
                if (newStock.ContainerInTile(tileRef.GridIndices))
                    return;

                newStock.SetTileMaxEntities(tileRef.GridIndices, Stock.DefaultMaxTileEntities + storage.Capacity);

                if (!AttachEntity(uid, newStock))
                    return;

                newStock.Containers.Add(uid);

                foreach (var ent in storage.Contents.ContainedEntities)
                {
                    foreach (var stock in Stockpiles)
                    {
                        if (DetachEntity(ent, stock))
                            break;
                    }

                    AttachEntity(ent, newStock);
                }
            }
            else
                AttachEntity(uid, newStock);
        }
    }
}
