using Content.Shared._RF.Stockpile;

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
        SubscribeNetworkEvent<StockpileSettingUpdate>(OnSettingUpdate);
        SubscribeNetworkEvent<StockpileSettingsUpdate>(OnSettingsUpdate);
        SubscribeNetworkEvent<StockpileEntityAttached>(OnAttachedEntity);
        SubscribeNetworkEvent<StockpileEntityDetached>(OnDetachedEntity);
    }

    private void OnMove(EntityUid uid, StockpileCategoryComponent component, MoveEvent args)
    {
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
}
