using System.Numerics;
using Content.Client._RF.NPC.Executable.Systems;
using Content.Client._RF.Selection;
using Content.Shared._RF.NPC.Executable.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Client._RF.NPC;

public sealed partial class NpcControlOverlay : Overlay
{
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private IPrototypeManager _prototype = default!;

    private static readonly ProtoId<ShaderPrototype> SelectShader = "DottedOutline";
    private static readonly ProtoId<ShaderPrototype> PointCircleShader = "DottedCircle";
    private static readonly ProtoId<ShaderPrototype> PointLineShader = "DottedLine";

    private readonly SelectionSystem _selection;
    private readonly SharedTransformSystem _transform;
    private readonly SpriteSystem _sprite;

    private readonly HashSet<SpriteComponent> _highlightedSprites = new();

    private readonly EntityQuery<ControllableNpcComponent> _controllableQuery;
    private readonly ExecutableGoalSystem _executable;

    public override bool RequestScreenTexture => true;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public NpcControlOverlay()
    {
        IoCManager.InjectDependencies(this);

        _selection = _entityManager.System<SelectionSystem>();
        _transform = _entityManager.System<SharedTransformSystem>();
        _sprite = _entityManager.System<SpriteSystem>();

        _controllableQuery = _entityManager.GetEntityQuery<ControllableNpcComponent>();
        _executable = _entityManager.System<ExecutableGoalSystem>();
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

        foreach (var entity in _selection.Selected<EntityUid>())
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

                // The way that the agent goes, if it is known, and a straight line if it is not (it is not going by a path, or the path is
                // not here yet).
                if (!DrawRoute(args, entity, controllable, start, proto.Color))
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

    /// <summary>
    /// The size of the dashes and of the line of the path, in pixels of the screen, so it looks the same at any zoom.
    /// </summary>
    private const float DashPixels = 10f;
    private const float GapPixels = 12f;
    private const float ThicknessPixels = 2f;

    /// <summary>
    /// How far from the agent and from the target the path begins and ends, in tiles: it does not lie over the sprite of the agent and over
    /// the circle of the target, as the straight line does not.
    /// </summary>
    private const float RouteOffset = 0.5f;

    /// <summary>
    /// Draws the rest of the path that the agent follows, from where it is now: the server tells the path when it is found and not while
    /// the agent goes along it, so how far it has got is found out from where it is. The dashes are counted from the beginning of
    /// the path and not from the agent, so they stand still while the agent goes by them and do not run.
    /// </summary>
    /// <returns>False if the agent has no path to draw.</returns>
    private bool DrawRoute(
        in OverlayDrawArgs args,
        EntityUid agent,
        ControllableNpcComponent controllable,
        MapCoordinates position,
        Color color)
    {
        if (controllable.Route.Count < 2)
        {
            // The server tells the path when it is changed. An agent that has come into view has not been told, and asks.
            _executable.RequestRoute(agent, controllable);
            return false;
        }

        if (!_entityManager.EntityExists(controllable.RouteGraph)
            || _transform.GetMapId(controllable.RouteGraph) != position.MapId)
            return false;

        var matrix = _transform.GetWorldMatrix(controllable.RouteGraph);
        var count = controllable.Route.Count;
        var points = new Vector2[count];
        var length = new float[count];

        for (var i = 0; i < count; i++)
        {
            points[i] = Vector2.Transform(controllable.Route[i], matrix);

            if (i > 0)
                length[i] = length[i - 1] + Vector2.Distance(points[i - 1], points[i]);
        }

        // How far along the path the agent is: by the segment it is the closest to.
        var from = position.Position;
        var best = float.MaxValue;
        var progress = 0f;

        for (var i = 0; i + 1 < count; i++)
        {
            var way = points[i + 1] - points[i];
            var squared = way.LengthSquared();
            var t = squared > 0f ? Math.Clamp(Vector2.Dot(from - points[i], way) / squared, 0f, 1f) : 0f;
            var distance = Vector2.DistanceSquared(from, points[i] + way * t);

            if (distance >= best)
                continue;

            best = distance;
            progress = length[i] + t * (length[i + 1] - length[i]);
        }

        var start = progress + RouteOffset;
        var end = length[^1] - RouteOffset;

        if (end <= start)
            return true;

        // Pixels in a tile.
        var unit = (args.Viewport.WorldToLocal(from + Vector2.UnitX) - args.Viewport.WorldToLocal(from)).X;

        if (unit <= 0.01f)
            return true;

        var dash = DashPixels / unit;
        var period = dash + GapPixels / unit;
        var thickness = ThicknessPixels / unit;
        var color2 = color.WithAlpha(0.9f);

        for (var k = (int) MathF.Floor(start / period); k * period < end; k++)
        {
            var from2 = Math.Max(k * period, start);
            var to = Math.Min(k * period + dash, end);

            if (to > from2)
                DrawRoutePiece(args.WorldHandle, points, length, from2, to, thickness, color2);
        }

        return true;
    }

    /// <summary>
    /// Draws the part of the path between two distances from its beginning: the part that is across a corner is drawn as two.
    /// </summary>
    private static void DrawRoutePiece(
        DrawingHandleWorld handle,
        Vector2[] points,
        float[] length,
        float from,
        float to,
        float thickness,
        Color color)
    {
        for (var i = 0; i + 1 < points.Length; i++)
        {
            var a = Math.Max(from, length[i]);
            var b = Math.Min(to, length[i + 1]);

            if (b <= a)
                continue;

            var segment = length[i + 1] - length[i];

            if (segment <= 0f)
                continue;

            var direction = (points[i + 1] - points[i]) / segment;
            var begin = points[i] + direction * (a - length[i]);
            var finish = points[i] + direction * (b - length[i]);
            var center = (begin + finish) / 2f;
            var box = Box2.CenteredAround(center, new Vector2(b - a, thickness));

            handle.DrawRect(new Box2Rotated(box, direction.ToAngle(), center), color);
        }
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

            if (proto.Icon == null)
                continue;

            var icon = _sprite.Frame0(proto.Icon);
            var box = Box2.CenteredAround(xform.Coordinates.Position, new Vector2(0.5f));
            var color = _prototype.Index(proto.Goal).Color.WithAlpha(0.6f);

            args.WorldHandle.DrawRect(box, Color.Black.WithAlpha(0.3f));
            args.WorldHandle.DrawTextureRect(icon, box, color);
        }
    }
}
