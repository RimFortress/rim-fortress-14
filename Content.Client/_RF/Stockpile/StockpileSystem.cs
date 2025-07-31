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

        SubscribeNetworkEvent<StockpileEntityAttached>(OnAttachedEntity);
        SubscribeNetworkEvent<StockpileEntityDetached>(OnDetachedEntity);

        _overlay.AddOverlay(new StockpileOverlay(this));
    }

    private void OnAttachedEntity(StockpileEntityAttached ev, EntitySessionEventArgs args)
    {
        if (!TryGetStock(ev.Id, out var stock) || stock.Owner != args.SenderSession.AttachedEntity)
            return;

        AttachEntity(GetEntity(ev.Uid), stock, false);
    }

    private void OnDetachedEntity(StockpileEntityDetached ev, EntitySessionEventArgs args)
    {
        if (!TryGetStock(ev.Id, out var stock) || stock.Owner != args.SenderSession.AttachedEntity)
            return;

        DetachEntity(GetEntity(ev.Uid), stock, false);
    }
}
