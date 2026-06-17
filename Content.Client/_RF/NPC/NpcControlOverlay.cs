using System.Numerics;
using Content.Client._RF.NPC.UtilityAi.Systems;
using Content.Client._RF.Selection;
using Content.Shared._RF.NPC.Components;
using Content.Shared._RF.NPC.UtilityAi.Systems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._RF.NPC;

public sealed class NpcControlOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    private static readonly ProtoId<ShaderPrototype> SelectShader = "DottedOutline";
    private static readonly ProtoId<ShaderPrototype> PointCircleShader = "DottedCircle";
    private static readonly ProtoId<ShaderPrototype> PointLineShader = "DottedLine";

    private readonly ExecutableGoalSystem _executable;
    private readonly SharedUtilityAiSystem _utilityAi;
    private readonly SelectionSystem _selection;
    private readonly SharedTransformSystem _transform;
    private readonly SpriteSystem _sprite;

    private readonly HashSet<SpriteComponent> _highlightedSprites = new();

    private readonly EntityQuery<TransformComponent> _transformQuery;
    private readonly EntityQuery<ControllableNpcComponent> _controllableQuery;

    public override bool RequestScreenTexture => true;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public NpcControlOverlay()
    {
        IoCManager.InjectDependencies(this);

        _executable = _entityManager.System<ExecutableGoalSystem>();
        _utilityAi = _entityManager.System<SharedUtilityAiSystem>();
        _selection = _entityManager.System<SelectionSystem>();
        _transform = _entityManager.System<SharedTransformSystem>();
        _sprite = _entityManager.System<SpriteSystem>();

        _transformQuery = _entityManager.GetEntityQuery<TransformComponent>();
        _controllableQuery = _entityManager.GetEntityQuery<ControllableNpcComponent>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        foreach (var sprite in _highlightedSprites)
        {
            sprite.PostShader = null;
            sprite.RenderOrder = 0;
        }

        _highlightedSprites.Clear();

        DrawPassiveGoals(args);

        foreach (var entity in _selection.Selected)
        {
            if (!_controllableQuery.HasComp(entity)
                || !_transformQuery.TryComp(entity, out var entityForm)
                || !_utilityAi.TryGetCurrentGoal(entity, out var goal)
                || !_prototype.Resolve(goal, out var proto))
                continue;

            if (!_executable.TryGetTargetCoordinates(entity, out var coords))
                return;

            var start = _transform.ToMapCoordinates(entityForm.Coordinates);
            var end = _transform.ToMapCoordinates(coords.Value);
            var dist = (end.Position - start.Position).Length();

            if (_executable.TryGetTarget(entity, out var uid))
                SetShader(uid.Value, proto.Color);
            else if (dist > 0.5f)
                DrawPointCircle(args, end, proto.Color);

            DrawLine(args, proto.Color, start, end);
        }
    }

    private void SetShader(EntityUid entity, Color color)
    {
        if (!_entityManager.TryGetComponent(entity, out SpriteComponent? sprite)
            || _highlightedSprites.Contains(sprite)
            || !sprite.Visible)
            return;

        var shader = _prototype.Index(SelectShader).InstanceUnique();
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

        var shader = _prototype.Index(PointLineShader).InstanceUnique();
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
        args.WorldHandle.DrawRect(new Box2(start.Position, end.Position), Color.White);
        args.WorldHandle.UseShader(prevShader);
    }

    private void DrawPointCircle(in OverlayDrawArgs args, MapCoordinates worldCoords, Color color)
    {
        var shader = _prototype.Index(PointCircleShader).InstanceUnique();
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

    private void DrawPassiveGoals(in OverlayDrawArgs args)
    {
        var enumerator = _entityManager.EntityQueryEnumerator<TransformComponent, PassiveGoalTargetComponent>();
        while (enumerator.MoveNext(out var xform, out var comp))
        {
            var proto = _prototype.Index(comp.Goal);

            if (proto.VerbIcon == null)
                continue;

            var icon = _sprite.Frame0(new SpriteSpecifier.Texture(proto.VerbIcon.Value));
            var box = Box2.CenteredAround(xform.Coordinates.Position, new Vector2(0.5f));
            var color = _prototype.Index(proto.Goal).Color.WithAlpha(0.6f);

            args.WorldHandle.DrawRect(box, Color.Black.WithAlpha(0.3f));
            args.WorldHandle.DrawTextureRect(icon, box, color);
        }
    }
}
