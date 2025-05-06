using System.Numerics;
using Content.Client.Stylesheets;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.ResourceManagement;
using Robust.Client.Utility;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._RF.NPC;

public sealed class NpcControlOverlay : Overlay
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
    [ValidatePrototypeId<ShaderPrototype>]
    private const string PointCircleShader = "DottedCircle";
    [ValidatePrototypeId<ShaderPrototype>]
    private const string PointLineShader = "DottedLine";

    private readonly NpcControlSystem _npcControl;
    private readonly SharedTransformSystem _transform;

    private readonly ShaderInstance _selectAreaShader;

    private readonly Color _selectColor = Color.LightGray;
    private readonly HashSet<SpriteComponent> _highlightedSprites = new();

    private readonly EntityQuery<TransformComponent> _transformQuery;

    public override bool RequestScreenTexture => true;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public NpcControlOverlay()
    {
        IoCManager.InjectDependencies(this);

        _npcControl = _entityManager.System<NpcControlSystem>();
        _transform = _entityManager.System<SharedTransformSystem>();

        _selectAreaShader = _prototype.Index<ShaderPrototype>(SelectAreaShader).InstanceUnique();
        _transformQuery = _entityManager.GetEntityQuery<TransformComponent>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        foreach (var sprite in _highlightedSprites)
        {
            sprite.PostShader = null;
            sprite.RenderOrder = 0;
        }

        _highlightedSprites.Clear();

        if (_npcControl is { StartPoint: { } startPoint, EndPoint: { } endPoint })
            DrawSelectArea(args, startPoint, endPoint);

        DrawPassiveTasks(args);

        if (_npcControl.SelectedTask is { IconPath: not null } selectedTask)
            DrawMouseIcon(args, selectedTask.IconPath, selectedTask.Color);

        if (_npcControl.Eraser)
            DrawMouseIcon(args, "/Textures/_RF/Interface/VerbIcons/eraser-solid.svg.192dpi.png", Color.White);

        foreach (var entity in _npcControl.Selected)
        {
            SetShader(entity, _selectColor);

            if (!_npcControl.Tasks.TryGetValue(entity, out var task)
                || !_transformQuery.TryComp(entity, out var entityForm))
                continue;

            if (!_transformQuery.TryComp(task.Target, out var xform)
                && task.Coordinates == null)
                return;

            var coords = task.Coordinates ?? xform!.Coordinates;
            var start = _transform.ToMapCoordinates(entityForm.Coordinates);
            var end = _transform.ToMapCoordinates(coords);
            var dist = (end.Position - start.Position).Length();

            if (task.Target != null)
            {
                SetShader(task.Target.Value, task.Color);
            }
            else
            {
                if (dist > 0.5f)
                    DrawPointCircle(args, end, task.Color);
            }

            DrawLine(args, task.Color, start, end);
        }
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

    private void DrawLine(
        in OverlayDrawArgs args,
        Color color,
        MapCoordinates start,
        MapCoordinates end)
    {
        if (start.Position == Vector2.Zero // Probably out-of-sight entity
            || end.Position == Vector2.Zero)
            return;

        var shader = _prototype.Index<ShaderPrototype>(PointLineShader).InstanceUnique();
        args.WorldHandle.UseShader(shader);

        var screenEnd = args.Viewport.WorldToLocal(end.Position);
        screenEnd.Y = args.Viewport.Size.Y - screenEnd.Y;

        var screeStart = args.Viewport.WorldToLocal(start.Position);
        screeStart.Y = args.Viewport.Size.Y - screeStart.Y;

        // Find the number of pixels in the coordinate unit to scale the size correctly.
        // It can probably be done in a better way, but my head is about to explode
        var unit = (args.Viewport.WorldToLocal(start.Position + Vector2.UnitX) - args.Viewport.WorldToLocal(start.Position)).X;
        shader.SetParameter("unit", unit);

        shader.SetParameter("color", color);
        shader.SetParameter("start", screenEnd);
        shader.SetParameter("end", screeStart);

        args.WorldHandle.DrawRect(new Box2(start.Position, end.Position), Color.White);
    }

    private void DrawPointCircle(in OverlayDrawArgs args, MapCoordinates worldCoords, Color color)
    {
        var shader = _prototype.Index<ShaderPrototype>(PointCircleShader).InstanceUnique();
        args.WorldHandle.UseShader(shader);

        var position = args.Viewport.WorldToLocal(worldCoords.Position);
        var unit = (args.Viewport.WorldToLocal(worldCoords.Position + Vector2.UnitX) - position).X;
        shader.SetParameter("unit", unit);

        position.Y = args.Viewport.Size.Y - position.Y;
        shader.SetParameter("position", position);

        shader.SetParameter("color", color);

        args.WorldHandle.DrawRect(Box2.CenteredAround(worldCoords.Position, Vector2.One), Color.White);
    }

    private void DrawSelectArea(in OverlayDrawArgs args, MapCoordinates start, MapCoordinates end)
    {
        var area = new Box2(start.Position, end.Position);

        var bottomLeft = args.Viewport.WorldToLocal(area.BottomLeft);
        bottomLeft.Y = args.Viewport.Size.Y - bottomLeft.Y;
        var bottomRight = args.Viewport.WorldToLocal(area.BottomRight);
        bottomRight.Y = args.Viewport.Size.Y - bottomRight.Y;

        var topLeft = args.Viewport.WorldToLocal(area.TopLeft);
        topLeft.Y = args.Viewport.Size.Y - topLeft.Y;
        var topRight = args.Viewport.WorldToLocal(area.TopRight);
        topRight.Y = args.Viewport.Size.Y - topRight.Y;

        var color = _npcControl.SelectedTask?.Color ?? _selectColor;

        _selectAreaShader.SetParameter("color", color);
        _selectAreaShader.SetParameter("point1", bottomLeft);
        _selectAreaShader.SetParameter("point2", bottomRight);
        _selectAreaShader.SetParameter("point3", topLeft);
        _selectAreaShader.SetParameter("point4", topRight);

        args.WorldHandle.UseShader(_selectAreaShader);
        args.WorldHandle.DrawRect(area, Color.White);
        args.WorldHandle.UseShader(null);
    }

    private void DrawPassiveTasks(in OverlayDrawArgs args)
    {
        foreach (var (task, targets) in _npcControl.PassiveTasks)
        {
            if (task.IconPath == null)
                continue;

            foreach (var target in targets)
            {
                if (!_transformQuery.TryComp(target, out var xform))
                    continue;

                var icon = new SpriteSpecifier.Texture(new ResPath(task.IconPath)).GetTexture(_resourceCache);
                var box = Box2.CenteredAround(xform.Coordinates.Position, new Vector2(0.5f));

                args.WorldHandle.DrawRect(box, StyleNano.PanelDark.WithAlpha(0.3f));
                args.WorldHandle.DrawTextureRect(icon, box, task.Color.WithAlpha(0.6f));
            }
        }
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
