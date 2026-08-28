using System.Numerics;
using Content.Client._RF.Selection;
using Content.Shared._RF.NPC.Executable.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Client._RF.NPC;

public sealed class NpcControlOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    private static readonly ProtoId<ShaderPrototype> SelectShader = "DottedOutline";
    private static readonly ProtoId<ShaderPrototype> PointCircleShader = "DottedCircle";
    private static readonly ProtoId<ShaderPrototype> PointLineShader = "DottedLine";

    private readonly SelectionSystem _selection;
    private readonly SharedTransformSystem _transform;
    private readonly SpriteSystem _sprite;

    private readonly HashSet<SpriteComponent> _highlightedSprites = new();

    private readonly EntityQuery<ControllableNpcComponent> _controllableQuery;

    public override bool RequestScreenTexture => true;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public NpcControlOverlay()
    {
        IoCManager.InjectDependencies(this);

        _selection = _entityManager.System<SelectionSystem>();
        _transform = _entityManager.System<SharedTransformSystem>();
        _sprite = _entityManager.System<SpriteSystem>();

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

        foreach (var entity in _selection.SelectedEntities())
        {
            if (!_controllableQuery.TryComp(entity, out var controllable)
                || !_prototype.TryIndex(controllable.CurrentGoal, out var exec)
                || !_prototype.Resolve(exec.Goal, out var proto))
                continue;

            MapCoordinates lastPos;

            if (controllable.CurrentTargetCoordinates is { } coords)
            {
                var start = _transform.GetMapCoordinates(entity);
                var end = _transform.ToMapCoordinates(coords);
                var dist = (end.Position - start.Position).Length();

                if (controllable.CurrentTarget is { } uid)
                    SetShader(uid, proto.Color);
                else if (dist > 0.5f)
                    DrawPointCircle(args, end, proto.Color);

                DrawLine(args, proto.Color, start, end);
                lastPos = end;
            }
            else
                lastPos = _transform.GetMapCoordinates(entity);

            foreach (var entry in controllable.Queue)
            {
                if (!_prototype.Resolve(entry.Goal, out var entryExecProto)
                    || !_prototype.Resolve(entryExecProto.Goal, out var entryProto))
                    continue;

                if (entry.Target == null && entry.TargetCoordinates == null)
                    continue;

                var targetUid = _entityManager.GetEntity(entry.Target);
                var mapPos = targetUid != null
                    ? _transform.GetMapCoordinates(targetUid.Value)
                    : _transform.ToMapCoordinates(entry.TargetCoordinates!.Value);

                if (targetUid != null)
                    SetShader(targetUid.Value, entryProto.Color);
                else
                    DrawPointCircle(args, mapPos, entryProto.Color);

                DrawLine(args, entryProto.Color, lastPos, mapPos);
                lastPos = mapPos;
            }
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

        var box = new Box2(
                Math.Min(start.X, end.X),
                Math.Min(start.Y, end.Y),
                Math.Max(start.X, end.X),
                Math.Max(start.Y, end.Y))
            .Enlarged(1f);

        args.WorldHandle.UseShader(shader);
        args.WorldHandle.DrawRect(box, Color.White);
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

            var icon = _sprite.Frame0(proto.VerbIcon);
            var box = Box2.CenteredAround(xform.Coordinates.Position, new Vector2(0.5f));
            var color = _prototype.Index(proto.Goal).Color.WithAlpha(0.6f);

            args.WorldHandle.DrawRect(box, Color.Black.WithAlpha(0.3f));
            args.WorldHandle.DrawTextureRect(icon, box, color);
        }
    }
}
