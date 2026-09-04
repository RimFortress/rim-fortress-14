using System.Numerics;
using Content.Client._RF.UserInterface.Controllers;
using Content.Shared._RF.NPC.Systems;
using Content.Shared._RF.Stockpile.Components;
using Content.Shared._RF.Stockpile.Systems;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Client._RF.Stockpile;

public sealed partial class StockpileOverlay : GridOverlay
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IEntityManager _entity = default!;

    private StockpileSystem _stockpile = default!;
    private readonly StockpileUiController _stockpileController;

    private const float BorderSize = 0.03f;
    private const float SelectedBorderSize = 0.05f;
    private const float SelectedBorderColorDelta = 0.05f;

    private static readonly ProtoId<ShaderPrototype> LineShader = "AnimatedDottedLine";

    private readonly Color _mainColor = Color.LightGray.WithAlpha(0.3f);
    private readonly Color _secondaryColor = Color.DarkGray.WithAlpha(0.3f);

    private readonly Color _selectedMainColor = Color.LightGray.WithAlpha(0.5f);
    private readonly Color _selectedSecondaryColor = Color.DarkGray.WithAlpha(0.5f);

    private readonly Color _supplyingLineColor = Color.BurlyWood;
    private readonly Color _suppliedLineColor = Color.Aquamarine;
    private readonly Color _newSuppliedLineColor = Color.MediumAquamarine;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceGrids;

    public StockpileOverlay()
    {
        IoCManager.InjectDependencies(this);

        _stockpileController = _ui.GetUIController<StockpileUiController>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_player.LocalEntity is not { } owner)
            return;

        _stockpile = _entity.System<StockpileSystem>();
        var ownership = _entity.System<OwnershipSystem>();

        foreach (var uid in ownership.GetOwned(owner))
        {
            if (!_entity.TryGetComponent(uid, out StockpileComponent? stock))
                continue;

            var ent = new Entity<StockpileComponent>(uid, stock);
            var selected = _stockpileController.HighlightedStockpiles.Contains(uid);
            var borderSize = selected ? SelectedBorderSize : BorderSize;
            var borderColor = selected
                ? new(
                    stock.Color.R + SelectedBorderColorDelta,
                    stock.Color.G + SelectedBorderColorDelta,
                    stock.Color.B + SelectedBorderColorDelta)
                : stock.Color;

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
                if (!StockpileSystem.TileInStock(ent, tile + Vector2i.Up))
                {
                    var box = Box2.FromDimensions(tile + new Vector2(0, 1f - borderSize), new Vector2(1f, borderSize));
                    args.WorldHandle.DrawRect(box, borderColor);
                }

                if (!StockpileSystem.TileInStock(ent, tile + Vector2i.Down))
                {
                    var box = Box2.FromDimensions(tile, new Vector2(1f, borderSize));
                    args.WorldHandle.DrawRect(box, borderColor);
                }

                if (!StockpileSystem.TileInStock(ent, tile + Vector2i.Left))
                {
                    var box = Box2.FromDimensions(tile, new Vector2(borderSize, 1f));
                    args.WorldHandle.DrawRect(box, borderColor);
                }

                if (!StockpileSystem.TileInStock(ent, tile + Vector2i.Right))
                {
                    var box = Box2.FromDimensions(tile + new Vector2(1f - borderSize, 0), new Vector2(borderSize, 1f));
                    args.WorldHandle.DrawRect(box, borderColor);
                }

                if (!StockpileSystem.TileInStock(ent, tile + Vector2i.UpLeft))
                {
                    var box = Box2.FromDimensions(tile + new Vector2(0, 1f - borderSize), new Vector2(borderSize));
                    args.WorldHandle.DrawRect(box, borderColor);
                }

                if (!StockpileSystem.TileInStock(ent, tile + Vector2i.UpRight))
                {
                    var box = Box2.FromDimensions(tile + new Vector2(1f - borderSize), new Vector2(borderSize));
                    args.WorldHandle.DrawRect(box, borderColor);
                }

                if (!StockpileSystem.TileInStock(ent, tile + Vector2i.DownLeft))
                {
                    var box = Box2.FromDimensions(tile, new Vector2(borderSize));
                    args.WorldHandle.DrawRect(box, borderColor);
                }

                if (!StockpileSystem.TileInStock(ent, tile + Vector2i.DownRight))
                {
                    var box = Box2.FromDimensions(tile + new Vector2(1f - borderSize, 0), new Vector2(borderSize));
                    args.WorldHandle.DrawRect(box, borderColor);
                }
            }

            foreach (var supplied in stock.Supplied)
            {
                if (!_entity.TryGetComponent(supplied, out StockpileComponent? suppliedComp))
                    continue;

                var center = _stockpile.StockCenter(ent);
                var suppliedCenter = _stockpile.StockCenter(new(supplied, suppliedComp));

                if (selected)
                    DrawLine(args, center, suppliedCenter, _supplyingLineColor);
                else if (_stockpileController.HighlightedStockpiles.Contains(supplied))
                    DrawLine(args, center, suppliedCenter, _suppliedLineColor);
            }
        }

        if (_stockpileController.DrawSupplyLine is { } line)
            DrawLine(args, line.Start, line.End, _newSuppliedLineColor);
    }

    private void DrawLine(in OverlayDrawArgs args, EntityCoordinates start, EntityCoordinates end, Color color)
    {
        var shader = _prototype.Index(LineShader).InstanceUnique();
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
