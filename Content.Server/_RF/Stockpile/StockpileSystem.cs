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
        SubscribeLocalEvent<StockpileCategoryComponent, ComponentShutdown>(OnShutdown);

        SubscribeNetworkEvent<StockpileCreated>(OnCreated);
        SubscribeNetworkEvent<StockpileDeleted>(OnDeleted);
        SubscribeNetworkEvent<StockpileTileAdded>(OnTileAdded);
        SubscribeNetworkEvent<StockpileTileRemoved>(OnTileRemoved);
        SubscribeNetworkEvent<StockpileSettingUpdated>(OnSettingUpdate);
        SubscribeNetworkEvent<StockpileSettingsUpdated>(OnSettingsUpdate);
    }

    private void OnMove(EntityUid uid, StockpileCategoryComponent component, MoveEvent args)
    {
        // TODO: optimization (spam on client with net events)
        foreach (var stock in Stockpiles)
        {
            if (DetachEntity(uid, stock))
                break;
        }

        if (TryGetStock(uid, out var stockpile))
            TryInsert(uid, stockpile);
    }

    private void OnShutdown(EntityUid uid, StockpileCategoryComponent component, ComponentShutdown args)
    {
        foreach (var stock in Stockpiles)
        {
            if (!stock.ContainsEntity(uid))
                continue;

            DetachEntity(uid, stock);
            return;
        }
    }

    private bool TryInsert(EntityUid uid, Stock stockpile)
    {
        if (Xform.GetGrid(uid) is not { } gridUid
            || !TryComp(gridUid, out MapGridComponent? grid)
            || !Map.TryGetTileRef(gridUid, grid, Transform(uid).Coordinates, out var tileRef)
            || Turf.IsTileBlocked(tileRef, CollisionGroup.Impassable ^ CollisionGroup.HighImpassable))
            return false;

        if (TryComp(uid, out EntityStorageComponent? storage))
        {
            //stockpile.RemoveEntitiesFrom(tileRef.GridIndices);
            stockpile.SetTileMaxEntities(tileRef.GridIndices, Stock.DefaultMaxTileEntities + storage.Capacity);

            if (!AttachEntity(uid, stockpile))
                return false;

            stockpile.Containers.Add(uid);

            foreach (var ent in storage.Contents.ContainedEntities)
            {
                foreach (var stock in Stockpiles)
                {
                    if (DetachEntity(ent, stock))
                        break;
                }

                AttachEntity(ent, stockpile);
            }
        }
        else if (!AttachEntity(uid, stockpile))
            return false;

        return true;
    }
}
