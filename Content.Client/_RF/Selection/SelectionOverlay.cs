using System.Numerics;
using Content.Client.Stylesheets;
using Content.Shared.Maps;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.ResourceManagement;
using Robust.Client.Utility;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._RF.Selection;

public sealed class SelectionOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IEyeManager _eye = default!;

    [ValidatePrototypeId<ShaderPrototype>]
    private const string SelectShader = "DottedOutline";
    [ValidatePrototypeId<ShaderPrototype>]
    private const string SelectAreaShader = "DottedSquareOutline";

    private readonly SelectionSystem _selection;
    private readonly TurfSystem _turf;
    private readonly TransformSystem _transform;

    private readonly HashSet<SpriteComponent> _highlightedSprites = new();

    public override bool RequestScreenTexture => true;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public SelectionOverlay()
    {
        IoCManager.InjectDependencies(this);

        _selection = _entityManager.System<SelectionSystem>();
        _turf = _entityManager.System<TurfSystem>();
        _transform = _entityManager.System<TransformSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        foreach (var sprite in _highlightedSprites)
        {
            sprite.PostShader = null;
            sprite.RenderOrder = 0;
        }

        _highlightedSprites.Clear();

        foreach (var entity in _selection.Selected)
        {
            SetShader(entity, _selection.SelectionColor);
        }

        foreach (var tileRef in _selection.SelectedTiles)
        {
            var center = _transform.ToMapCoordinates(_turf.GetTileCenter(tileRef));
            var start = new MapCoordinates(center.Position + new Vector2(0.5f), center.MapId);
            var end = new MapCoordinates(center.Position - new Vector2(0.5f), center.MapId);

            DrawSelectArea(args, start, end);
        }

        if (_selection is { StartPoint: { } startPoint, EndPoint: { } endPoint })
            DrawSelectArea(args, startPoint, endPoint);

        if (_selection.IconPath != null)
            DrawMouseIcon(args, _selection.IconPath, _selection.IconColor);
    }

    private void SetShader(EntityUid entity, Color color)
    {
        if (!_entityManager.TryGetComponent(entity, out SpriteComponent? sprite)
            || _highlightedSprites.Contains(sprite)
            || !sprite.Visible)
            return;

        var shader = _prototype.Index<ShaderPrototype>(SelectShader).InstanceUnique();
        _highlightedSprites.Add(sprite);
        shader.SetParameter("color", color);

        sprite.PostShader = shader;
        sprite.RenderOrder = _entityManager.CurrentTick.Value;
    }

    private void DrawSelectArea(in OverlayDrawArgs args, MapCoordinates start, MapCoordinates end)
    {
        var shader = _prototype.Index<ShaderPrototype>(SelectAreaShader).InstanceUnique();
        var area = new Box2(start.Position, end.Position);
        var prevShader = args.WorldHandle.GetShader();

        var bottomLeft = args.Viewport.WorldToLocal(area.BottomLeft);
        bottomLeft.Y = args.Viewport.Size.Y - bottomLeft.Y;
        var bottomRight = args.Viewport.WorldToLocal(area.BottomRight);
        bottomRight.Y = args.Viewport.Size.Y - bottomRight.Y;

        var topLeft = args.Viewport.WorldToLocal(area.TopLeft);
        topLeft.Y = args.Viewport.Size.Y - topLeft.Y;
        var topRight = args.Viewport.WorldToLocal(area.TopRight);
        topRight.Y = args.Viewport.Size.Y - topRight.Y;

        shader.SetParameter("color", _selection.SelectionColor);
        shader.SetParameter("point1", bottomLeft);
        shader.SetParameter("point2", bottomRight);
        shader.SetParameter("point3", topLeft);
        shader.SetParameter("point4", topRight);

        args.WorldHandle.UseShader(shader);
        args.WorldHandle.DrawRect(area, Color.White);
        args.WorldHandle.UseShader(prevShader);
    }

    private void DrawMouseIcon(in OverlayDrawArgs args, string path, Color color)
    {
        if (_input.MouseScreenPosition is not { IsValid: true } mousePos)
            return;

        var size = 0.5f;
        var mapPos = _eye.PixelToMap(mousePos);

        if (mapPos.Position == Vector2.Zero)
            return;

        var icon = new SpriteSpecifier.Texture(new ResPath(path)).GetTexture(_resourceCache);
        var box = new Box2(new Vector2(mapPos.X, mapPos.Y - size), new Vector2(mapPos.X + size, mapPos.Y));

        args.WorldHandle.DrawRect(box, StyleNano.PanelDark.WithAlpha(0.6f));
        args.WorldHandle.DrawTextureRect(icon, box, color);
    }
}
