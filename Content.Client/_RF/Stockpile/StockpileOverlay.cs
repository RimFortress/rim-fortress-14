using System.Numerics;
using Robust.Client.Graphics;
using Robust.Shared.Enums;

namespace Content.Client._RF.Stockpile;

public sealed class StockpileOverlay(StockpileSystem stockpile) : Overlay
{
    private readonly StockpileSystem _stockpile = stockpile;

    private readonly Color _mainColor = Color.LightGray.WithAlpha(0.6f);
    private readonly Color _secondaryColor = Color.DarkGray.WithAlpha(0.6f);

    public override OverlaySpace Space => OverlaySpace.WorldSpaceGrids;

    protected override void Draw(in OverlayDrawArgs args)
    {
        foreach (var (grid, tiles) in _stockpile.AllStockpileTiles())
        {
            foreach (var tile in tiles)
            {
                var size = new Vector2(0.5f) * grid.Comp.TileSize;
                var lbBox = Box2.FromDimensions(tile, size);
                var ltBox = Box2.FromDimensions(tile + new Vector2(0, 0.5f), size);
                var rbBox = Box2.FromDimensions(tile + new Vector2(0.5f, 0), size);
                var rtBox = Box2.FromDimensions(tile + new Vector2(0.5f), size);

                args.WorldHandle.DrawRect(lbBox, _mainColor);
                args.WorldHandle.DrawRect(ltBox, _secondaryColor);
                args.WorldHandle.DrawRect(rbBox, _secondaryColor);
                args.WorldHandle.DrawRect(rtBox, _mainColor);
            }
        }
    }
}
