using System.Numerics;
using Content.Client._RF.UserInterface.Controls.Stockpile;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Client._RF.Stockpile;

public sealed class StockpileOverlay : GridOverlay
{
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    private readonly StockpileSystem _stockpile;
    private readonly StockpileUiController _stockpileController;

    private const float BorderSize = 0.03f;
    private const float SelectedBorderSize = 0.05f;

    [ValidatePrototypeId<ShaderPrototype>]
    private const string LineShader = "AnimatedDottedLine";

    private readonly Color _mainColor = Color.LightGray.WithAlpha(0.3f);
    private readonly Color _secondaryColor = Color.DarkGray.WithAlpha(0.3f);

    private readonly Color _selectedMainColor = Color.LightGray.WithAlpha(0.5f);
    private readonly Color _selectedSecondaryColor = Color.DarkGray.WithAlpha(0.5f);

    private readonly Color _borderColor = Color.DarkOrange;
    private readonly Color _selectedBorderColor = Color.Orange;

    private readonly Color _supplyingLineColor = Color.BurlyWood;
    private readonly Color _suppliedLineColor = Color.Aquamarine;
    private readonly Color _newSuppliedLineColor = Color.MediumAquamarine;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceGrids;

    public StockpileOverlay(StockpileSystem stockpile)
    {
        IoCManager.InjectDependencies(this);

        _stockpile = stockpile;
        _stockpileController = _ui.GetUIController<StockpileUiController>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        foreach (var stock in _stockpile.AllStockpiles())
        {
            var selected = _stockpileController.HighlightedStockpiles.Contains(stock.Id);
            var borderSize = selected ? SelectedBorderSize : BorderSize;
            var borderColor = selected ? _selectedBorderColor : _borderColor;

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
                    var box = Box2.FromDimensions(tile + new Vector2(0, 1f - borderSize), new Vector2(1f, borderSize));
                    args.WorldHandle.DrawRect(box, borderColor);
                }

                if (!stock.Tiles.Contains(tile + Vector2i.Down))
                {
                    var box = Box2.FromDimensions(tile, new Vector2(1f, borderSize));
                    args.WorldHandle.DrawRect(box, borderColor);
                }

                if (!stock.Tiles.Contains(tile + Vector2i.Left))
                {
                    var box = Box2.FromDimensions(tile, new Vector2(borderSize, 1f));
                    args.WorldHandle.DrawRect(box, borderColor);
                }

                if (!stock.Tiles.Contains(tile + Vector2i.Right))
                {
                    var box = Box2.FromDimensions(tile + new Vector2(1f - borderSize, 0), new Vector2(borderSize, 1f));
                    args.WorldHandle.DrawRect(box, borderColor);
                }

                if (!stock.Tiles.Contains(tile + Vector2i.UpLeft))
                {
                    var box = Box2.FromDimensions(tile + new Vector2(0, 1f - borderSize), new Vector2(borderSize));
                    args.WorldHandle.DrawRect(box, borderColor);
                }

                if (!stock.Tiles.Contains(tile + Vector2i.UpRight))
                {
                    var box = Box2.FromDimensions(tile + new Vector2(1f - borderSize), new Vector2(borderSize));
                    args.WorldHandle.DrawRect(box, borderColor);
                }

                if (!stock.Tiles.Contains(tile + Vector2i.DownLeft))
                {
                    var box = Box2.FromDimensions(tile, new Vector2(borderSize));
                    args.WorldHandle.DrawRect(box, borderColor);
                }

                if (!stock.Tiles.Contains(tile + Vector2i.DownRight))
                {
                    var box = Box2.FromDimensions(tile + new Vector2(1f - borderSize, 0), new Vector2(borderSize));
                    args.WorldHandle.DrawRect(box, borderColor);
                }
            }

            foreach (var id in stock.SuppliedStockpiles)
            {
                if (selected)
                {
                    if (_stockpile.TryGetStock(id, out var supplied))
                        DrawLine(args, stock.CenterCoordinates(), supplied.CenterCoordinates(), _supplyingLineColor);
                }
                else if (_stockpileController.HighlightedStockpiles.Contains(id))
                {
                    if (_stockpile.TryGetStock(id, out var supplied))
                        DrawLine(args, stock.CenterCoordinates(), supplied.CenterCoordinates(), _suppliedLineColor);
                }
            }
        }

        if (_stockpileController.DrawSupplyLine is { } line)
            DrawLine(args, line.Start, line.End, _newSuppliedLineColor);
    }

    private void DrawLine(in OverlayDrawArgs args, EntityCoordinates start, EntityCoordinates end, Color color)
    {
        var shader = _prototype.Index<ShaderPrototype>(LineShader).InstanceUnique();
        var prevShader = args.WorldHandle.GetShader();

        var screenEnd = args.Viewport.WorldToLocal(end.Position);
        screenEnd.Y = args.Viewport.Size.Y - screenEnd.Y;

        var screenStart = args.Viewport.WorldToLocal(start.Position);
        screenStart.Y = args.Viewport.Size.Y - screenStart.Y;

        var unit = (args.Viewport.WorldToLocal(start.Position + Vector2.UnitX) - args.Viewport.WorldToLocal(start.Position)).X;
        shader.SetParameter("unit", unit);

        shader.SetParameter("color", color);
        shader.SetParameter("start", screenEnd);
        shader.SetParameter("end", screenStart);

        args.WorldHandle.UseShader(shader);
        args.WorldHandle.DrawRect(new Box2(start.Position, end.Position).Enlarged(-0.5f), Color.White);
        args.WorldHandle.UseShader(prevShader);
    }
}
