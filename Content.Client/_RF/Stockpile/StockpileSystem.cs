using Content.Shared._RF.Stockpile;
using Robust.Client.Graphics;

namespace Content.Client._RF.Stockpile;

public sealed class StockpileSystem : SharedStockpileSystem
{
    [Dependency] private readonly IOverlayManager _overlay = default!;

    public Stock? SelectedStock { get; set; }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<StockpileCreated>(OnCreated);
        SubscribeNetworkEvent<StockpileDeleted>(OnDeleted);
        SubscribeNetworkEvent<StockpileTileAdded>(OnTileAdded);
        SubscribeNetworkEvent<StockpileTileRemoved>(OnTileRemoved);
        SubscribeNetworkEvent<StockpileSettingUpdated>(OnSettingUpdate);
        SubscribeNetworkEvent<StockpileSettingsUpdated>(OnSettingsUpdate);
        SubscribeNetworkEvent<StockpileEntityAttached>(OnAttachedEntity);
        SubscribeNetworkEvent<StockpileEntityDetached>(OnDetachedEntity);

        _overlay.AddOverlay(new StockpileOverlay(this));
    }
}
