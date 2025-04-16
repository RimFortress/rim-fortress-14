using System.Numerics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Client._RF.NPC;

public sealed class NpcControlOverlay : Overlay
{
    public override bool RequestScreenTexture => true;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    [ValidatePrototypeId<ShaderPrototype>]
    private const string SelectShader = "DottedOutline";
    [ValidatePrototypeId<ShaderPrototype>]
    private const string SelectAreaShader = "DottedSquareOutline";
    [ValidatePrototypeId<ShaderPrototype>]
    private const string PointCircleShader = "DottedCircle";
    [ValidatePrototypeId<ShaderPrototype>]
    private const string PointLineShader = "DottedLine";

    private readonly Color _selectColor = Color.LightGray;

    private readonly NpcControlSystem _npcControl;
    private readonly SharedTransformSystem _transform;
    private readonly IEntityManager _entityManager;
    private readonly IPrototypeManager _prototype;

    private readonly ShaderInstance _selectAreaShader;

    private readonly HashSet<SpriteComponent> _highlightedSprites = new();

    public NpcControlOverlay(
        IPrototypeManager prototype,
        IEntityManager entityManager,
        IEntitySystemManager entSysManager)
    {
        _npcControl = entSysManager.GetEntitySystem<NpcControlSystem>();
        _transform = entSysManager.GetEntitySystem<SharedTransformSystem>();

        _entityManager = entityManager;
        _prototype = prototype;

        _selectAreaShader = _prototype.Index<ShaderPrototype>(SelectAreaShader).InstanceUnique();
        _selectAreaShader.SetParameter("color", _selectColor);
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
        {
            var area = new Box2(startPoint.Position, endPoint.Position);

            var bottomLeft = args.Viewport.WorldToLocal(area.BottomLeft);
            bottomLeft.Y = args.Viewport.Size.Y - bottomLeft.Y;
            var bottomRight = args.Viewport.WorldToLocal(area.BottomRight);
            bottomRight.Y = args.Viewport.Size.Y - bottomRight.Y;

            var topLeft = args.Viewport.WorldToLocal(area.TopLeft);
            topLeft.Y = args.Viewport.Size.Y - topLeft.Y;
            var topRight = args.Viewport.WorldToLocal(area.TopRight);
            topRight.Y = args.Viewport.Size.Y - topRight.Y;

            _selectAreaShader.SetParameter("point1", bottomLeft);
            _selectAreaShader.SetParameter("point2", bottomRight);
            _selectAreaShader.SetParameter("point3", topLeft);
            _selectAreaShader.SetParameter("point4", topRight);

            args.WorldHandle.UseShader(_selectAreaShader);
            args.WorldHandle.DrawRect(area, Color.White);
        }

        foreach (var entity in _npcControl.Selected)
        {
            SetShader(entity, _selectColor);

            if (!_npcControl.Tasks.TryGetValue(entity, out var task)
                || !_entityManager.TryGetComponent(entity, out TransformComponent? entityForm))
                continue;

            _entityManager.TryGetComponent(task.Target, out TransformComponent? xform);

            if (xform == null && task.Coordinates == null)
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
}
