using System.Numerics;
using Content.Client._RF.Selection;
using Content.Client.Stylesheets;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
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

    [ValidatePrototypeId<ShaderPrototype>]
    private const string SelectShader = "DottedOutline";
    [ValidatePrototypeId<ShaderPrototype>]
    private const string PointCircleShader = "DottedCircle";
    [ValidatePrototypeId<ShaderPrototype>]
    private const string PointLineShader = "DottedLine";

    private readonly NpcControlSystem _npcControl;
    private readonly SelectionSystem _selection;
    private readonly SharedTransformSystem _transform;

    private readonly HashSet<SpriteComponent> _highlightedSprites = new();

    private readonly EntityQuery<TransformComponent> _transformQuery;

    public override bool RequestScreenTexture => true;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public NpcControlOverlay()
    {
        IoCManager.InjectDependencies(this);

        _npcControl = _entityManager.System<NpcControlSystem>();
        _selection = _entityManager.System<SelectionSystem>();
        _transform = _entityManager.System<SharedTransformSystem>();

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

        DrawPassiveTasks(args);

        foreach (var entity in _selection.Selected)
        {
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
        var prevShader = args.WorldHandle.GetShader();

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

        args.WorldHandle.UseShader(shader);
        args.WorldHandle.DrawRect(new Box2(start.Position, end.Position).Enlarged(0.5f), Color.White);
        args.WorldHandle.UseShader(prevShader);
    }

    private void DrawPointCircle(in OverlayDrawArgs args, MapCoordinates worldCoords, Color color)
    {
        var shader = _prototype.Index<ShaderPrototype>(PointCircleShader).InstanceUnique();
        var prevShader = args.WorldHandle.GetShader();

        var position = args.Viewport.WorldToLocal(worldCoords.Position);
        var unit = (args.Viewport.WorldToLocal(worldCoords.Position + Vector2.UnitX) - position).X;
        shader.SetParameter("unit", unit);

        position.Y = args.Viewport.Size.Y - position.Y;
        shader.SetParameter("position", position);

        shader.SetParameter("color", color);

        args.WorldHandle.UseShader(shader);
        args.WorldHandle.DrawRect(Box2.CenteredAround(worldCoords.Position, Vector2.One), Color.White);
        args.WorldHandle.UseShader(prevShader);
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
}
