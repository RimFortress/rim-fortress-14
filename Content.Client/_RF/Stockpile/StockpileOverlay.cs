using System.Numerics;
using Robust.Client.Graphics;
using Robust.Shared.Enums;

namespace Content.Client._RF.Stockpile;

public sealed class StockpileOverlay(StockpileSystem stockpile) : GridOverlay
{
    private const float BorderSize = 0.05f;

    private readonly Color _mainColor = Color.LightGray.WithAlpha(0.3f);
    private readonly Color _secondaryColor = Color.DarkGray.WithAlpha(0.3f);
    private readonly Color _selectedMainColor = Color.LightGray.WithAlpha(0.5f);
    private readonly Color _selectedSecondaryColor = Color.DarkGray.WithAlpha(0.5f);
    private readonly Color _borderColor = Color.DarkOrange;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceGrids;

    protected override void Draw(in OverlayDrawArgs args)
    {
        foreach (var stock in stockpile.AllStockpiles())
        {
            var selected = stock.Id == stockpile.SelectedStock?.Id;

            foreach (var tile in stock.Tiles)
            {
                var size = new Vector2(0.5f);
                var lbBox = Box2.FromDimensions(tile, size);
                var ltBox = Box2.FromDimensions(tile + new Vector2(0, 0.5f), size);
                var rbBox = Box2.FromDimensions(tile + new Vector2(0.5f, 0), size);
                var rtBox = Box2.FromDimensions(tile + new Vector2(0.5f), size);

                args.WorldHandle.DrawRect(lbBox, selected ? _selectedMainColor : _mainColor);
                args.WorldHandle.DrawRect(ltBox, selected ? _selectedSecondaryColor : _secondaryColor);
                args.WorldHandle.DrawRect(rbBox, selected ? _selectedSecondaryColor : _secondaryColor);
                args.WorldHandle.DrawRect(rtBox, selected ? _selectedMainColor : _mainColor);

                // Borders drawing
                if (!stock.Tiles.Contains(tile + Vector2i.Up))
                {
                    var box = Box2.FromDimensions(tile + new Vector2(0, 1f - BorderSize), new Vector2(1f, BorderSize));
                    args.WorldHandle.DrawRect(box, _borderColor);
                }

                if (!stock.Tiles.Contains(tile + Vector2i.Down))
                {
                    var box = Box2.FromDimensions(tile, new Vector2(1f, BorderSize));
                    args.WorldHandle.DrawRect(box, _borderColor);
                }

                if (!stock.Tiles.Contains(tile + Vector2i.Left))
                {
                    var box = Box2.FromDimensions(tile, new Vector2(BorderSize, 1f));
                    args.WorldHandle.DrawRect(box, _borderColor);
                }

                if (!stock.Tiles.Contains(tile + Vector2i.Right))
                {
                    var box = Box2.FromDimensions(tile + new Vector2(1f - BorderSize, 0), new Vector2(BorderSize, 1f));
                    args.WorldHandle.DrawRect(box, _borderColor);
                }
            }
        }
    }
}
