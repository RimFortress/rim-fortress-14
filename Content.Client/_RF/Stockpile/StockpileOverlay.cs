using System.Numerics;
using Content.Client._RF.UserInterface.Controllers;
using Content.Shared._RF.NPC.Systems;
using Content.Shared._RF.Stockpile.Components;
using Content.Shared._RF.Zoning.Components;
using Content.Shared._RF.Zoning.Systems;
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

    private ZoningSystem? _zoning;
    private OwnershipSystem? _ownership;
    private readonly StockpileUiController _stockpileController;

    private static readonly ProtoId<ShaderPrototype> LineShader = "AnimatedDottedLine";

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

        _zoning ??= _entity.System<ZoningSystem>();
        _ownership ??= _entity.System<OwnershipSystem>();

        foreach (var uid in _ownership.GetOwned(owner))
        {
            if (!_entity.TryGetComponent(uid, out StockpileComponent? stock)
                || !_entity.TryGetComponent(uid, out ZoneComponent? zone))
                continue;

            var ent = new Entity<ZoneComponent>(uid, zone);
            var selected = _stockpileController.HighlightedStockpiles.Contains(uid);

            foreach (var supplied in stock.Supplied)
            {
                if (!_entity.TryGetComponent(supplied, out ZoneComponent? suppliedZone))
                    continue;

                var center = _zoning.ZoneCenter(ent);
                var suppliedCenter = _zoning.ZoneCenter(new(supplied, suppliedZone));

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
