using System.Numerics;
using Content.Client._RF.Selection;
using Content.Shared._RF.Zoning.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._RF.Zoning.UI;

public sealed partial class ZoningOverlay : GridOverlay
{
    [Dependency] private IEntityManager _entity = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototype = default!;

    private TransformSystem? _transform;
    private SpriteSystem? _sprite;

    private static readonly ProtoId<ShaderPrototype> TileBorderShader = "DottedTileBorder";
    //private static readonly ProtoId<ShaderPrototype> SelectedTileBorderShader = "SelectedZoneBorder";
    private static readonly ProtoId<ShaderPrototype> TileGridShader = "DottedTileGrid";

    public override OverlaySpace Space => OverlaySpace.WorldSpaceGrids;

    public ZoningOverlay()
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var enumerator = _entity.EntityQueryEnumerator<ZoneComponent, ZoneVisualsComponent>();

        _transform ??= _entity.System<TransformSystem>();
        _sprite ??= _entity.System<SpriteSystem>();

        while (enumerator.MoveNext(out var uid, out var zone, out var comp))
        {
            var style = comp.GetStyle();

            if (_transform.GetGrid(uid) is not { } grid)
                continue;

            if (style.TileSprite is { } sprite)
                DrawTileSprites(args, zone.Tiles, sprite, style.ZoneColor);
            else
                SelectionOverlay.DrawTileInner(args, zone.Tiles, style.ZoneColor);

            SelectionOverlay.DrawTileBoundary(args,
                grid,
                zone.Tiles,
                style.BorderColor,
                _prototype.Index(TileBorderShader),
                _transform);

            if (style.ShowTileBorders != true)
                continue;

            SelectionOverlay.DrawInteriorGridLines(args,
                grid,
                zone.Tiles,
                style.BorderColor,
                _prototype.Index(TileGridShader),
                _transform);
        }
    }

    private void DrawTileSprites(
        in OverlayDrawArgs args,
        IEnumerable<Vector2i> tiles,
        SpriteSpecifier sprite,
        Color? modulate)
    {
        _sprite ??= _entity.System<SpriteSystem>();
        var texture = _sprite.GetFrame(sprite, _timing.CurTime);

        foreach (var tile in tiles)
        {
            var center = tile + new Vector2(0.5f, 0.5f);
            var start = center + new Vector2(0.5f);
            var end = center - new Vector2(0.5f);
            var area = new Box2(
                Math.Min(start.X, end.X),
                Math.Min(start.Y, end.Y),
                Math.Max(start.X, end.X),
                Math.Max(start.Y, end.Y));

            args.WorldHandle.DrawTextureRect(texture, area, modulate);
        }
    }
}
