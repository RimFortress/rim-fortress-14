using System.Linq;
using System.Numerics;
using Content.Client._RF.Stylesheets;
using Content.Shared._RF.Selection.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._RF.Selection;

public sealed partial class SelectionOverlay : Overlay
{
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IEyeManager _eye = default!;

    private static readonly ProtoId<ShaderPrototype> SelectShader = "DottedOutline";
    private static readonly ProtoId<ShaderPrototype> SelectAreaShader = "DottedSquareOutline";
    private static readonly ProtoId<ShaderPrototype> TileBorderShader = "DottedTileBorder";
    private static readonly ProtoId<ShaderPrototype> TileGridShader = "DottedTileGrid";

    private const string SelectionPostShaderId = "SelectionPostShader";

    private readonly TransformSystem _transform;
    private readonly SpriteSystem _sprite;
    private readonly SelectionSystem _selection;

    private readonly HashSet<SpriteComponent> _highlightedSprites = new();

    public override bool RequestScreenTexture => true;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public SelectionOverlay()
    {
        IoCManager.InjectDependencies(this);

        _transform = _entityManager.System<TransformSystem>();
        _sprite = _entityManager.System<SpriteSystem>();
        _selection = _entityManager.System<SelectionSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        foreach (var sprite in _highlightedSprites)
        {
            _sprite.RemovePostShader(sprite, SelectionPostShaderId);
            sprite.RenderOrder = 0;
        }

        _highlightedSprites.Clear();

        if (!_entityManager.TryGetComponent(_player.LocalEntity, out SelectionComponent? comp)
            || _selection.GetSelection() is not { } selection)
            return;

        foreach (var entity in _selection.Selected<EntityUid>())
        {
            SetShader(entity, selection.Color);
        }

        DrawTileInner(args, _selection.Selected<TileRef>().Select(x => x.GridIndices), selection.InnerColor);
        DrawTileSelection(args, _selection.Selected<TileRef>(), selection.Color);

        if (selection.ShowArea
            && comp is { StartPoint: { } startPoint, EndPoint: { } endPoint })
            DrawSelectArea(args, startPoint, endPoint, selection.Color);

        if (selection.Icon != null)
            DrawMouseIcon(args, selection.Icon, selection.IconColor);
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

        _sprite.SetPostShader(sprite, new SpriteComponent.PostShaderArgs(SelectionPostShaderId, shader));
        sprite.RenderOrder = _entityManager.CurrentTick.Value;
    }

    private void DrawSelectArea(in OverlayDrawArgs args, MapCoordinates start, MapCoordinates end, Color color)
    {
        var shader = _prototype.Index(SelectAreaShader).InstanceUnique();
        var area = new Box2(
            Math.Min(start.X, end.X),
            Math.Min(start.Y, end.Y),
            Math.Max(start.X, end.X),
            Math.Max(start.Y, end.Y));
        var prevShader = args.WorldHandle.GetShader();

        var bottomLeft = args.Viewport.WorldToLocal(area.BottomLeft);
        bottomLeft.Y = args.Viewport.Size.Y - bottomLeft.Y;
        var bottomRight = args.Viewport.WorldToLocal(area.BottomRight);
        bottomRight.Y = args.Viewport.Size.Y - bottomRight.Y;

        var topLeft = args.Viewport.WorldToLocal(area.TopLeft);
        topLeft.Y = args.Viewport.Size.Y - topLeft.Y;
        var topRight = args.Viewport.WorldToLocal(area.TopRight);
        topRight.Y = args.Viewport.Size.Y - topRight.Y;

        shader.SetParameter("color", color);
        shader.SetParameter("point1", bottomLeft);
        shader.SetParameter("point2", bottomRight);
        shader.SetParameter("point3", topLeft);
        shader.SetParameter("point4", topRight);

        area = area.Enlarged(1f);

        args.WorldHandle.UseShader(shader);
        args.WorldHandle.DrawRect(area, Color.White);
        args.WorldHandle.UseShader(prevShader);
    }

    private void DrawMouseIcon(in OverlayDrawArgs args, SpriteSpecifier sprite, Color color)
    {
        if (_input.MouseScreenPosition is not { IsValid: true } mousePos)
            return;

        var size = 0.5f;
        var mapPos = _eye.PixelToMap(mousePos);

        if (mapPos.Position == Vector2.Zero)
            return;

        var icon = _sprite.Frame0(sprite);
        var box = new Box2(new Vector2(mapPos.X, mapPos.Y - size), new Vector2(mapPos.X + size, mapPos.Y));

        args.WorldHandle.DrawRect(box, StyleFortress.BlackAmber.WithAlpha(0.6f));
        args.WorldHandle.DrawTextureRect(icon, box, color);
    }

    #region Tiles

    public static void DrawTileInner( in OverlayDrawArgs args, IEnumerable<Vector2i> tiles, Color? color)
    {
        if (color == null)
            return;

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

            args.WorldHandle.DrawRect(area, color.Value);
        }
    }

    /// <summary>
    /// Draws the selected tiles as a merged region: a thick dashed outline where the
    /// selection actually ends, and thin plain lines on the grid edges shared between
    /// two adjacent selected tiles.
    /// </summary>
    private void DrawTileSelection(in OverlayDrawArgs args, IReadOnlySet<TileRef> tiles, Color color)
    {
        if (tiles.Count == 0)
            return;

        var gridShader = _prototype.Index(TileGridShader);
        var borderShader = _prototype.Index(TileBorderShader);

        // A selection normally lives on a single grid, but group defensively in case it doesn't.
        foreach (var group in tiles.GroupBy(t => t.GridUid))
        {
            var gridUid = group.Key;
            var indices = new HashSet<Vector2i>();

            foreach (var tile in group)
            {
                indices.Add(tile.GridIndices);
            }

            DrawInteriorGridLines(args, gridUid, indices, color, gridShader, _transform);
            DrawTileBoundary(args, gridUid, indices, color, borderShader, _transform);
        }
    }

    /// <summary>
    /// Thin lines on grid edges shared between two selected tiles.
    /// </summary>
    public static void DrawInteriorGridLines(
        in OverlayDrawArgs args,
        EntityUid gridUid,
        HashSet<Vector2i> tiles,
        Color? color,
        ShaderPrototype shader,
        TransformSystem transform)
    {
        if (color == null)
            return;

        var thinColor = color.Value.WithAlpha(0.6f);

        foreach (var tile in tiles)
        {
            // Checking only the right/up neighbour of every tile visits each interior
            // edge exactly once (the mirrored left/down edge belongs to that neighbour).
            if (tiles.Contains(tile + Vector2i.Right))
            {
                var a = GridCornerToMap(gridUid, tile + new Vector2i(1, 0), transform);
                var b = GridCornerToMap(gridUid, tile + new Vector2i(1, 1), transform);
                DrawDashedSegment(args, a, b, thinColor, shader);
            }

            if (tiles.Contains(tile + Vector2i.Up))
            {
                var a = GridCornerToMap(gridUid, tile + new Vector2i(0, 1), transform);
                var b = GridCornerToMap(gridUid, tile + new Vector2i(1, 1), transform);
                DrawDashedSegment(args, a, b, thinColor, shader);
            }
        }
    }

    /// <summary>
    /// Thick dashed outline along the actual boundary of the selected region (and any
    /// holes in it), with collinear runs collapsed so the dash pattern isn't reset at
    /// every single tile edge.
    /// </summary>
    public static void DrawTileBoundary(
        in OverlayDrawArgs args,
        EntityUid gridUid,
        HashSet<Vector2i> tiles,
        Color? color,
        ShaderPrototype shader,
        TransformSystem transform)
    {
        if (color == null)
            return;

        foreach (var loop in GetBoundaryLoops(tiles))
        {
            if (loop.Count < 2)
                continue;

            for (var i = 0; i < loop.Count; i++)
            {
                var a = GridCornerToMap(gridUid, loop[i], transform);
                var b = GridCornerToMap(gridUid, loop[(i + 1) % loop.Count], transform);
                DrawDashedSegment(args, a, b, color.Value, shader);
            }
        }
    }

    /// <summary>
    /// Traces the boundary of a tile region into closed loops of grid-corner points,
    /// with consecutive collinear edges merged into single segments.
    /// </summary>
    private static List<List<Vector2i>> GetBoundaryLoops(IReadOnlySet<Vector2i> tiles)
    {
        var outgoing = new Dictionary<Vector2i, List<Vector2i>>();

        foreach (var tile in tiles)
        {
            var br = tile + new Vector2i(1, 0);
            var tr = tile + new Vector2i(1, 1);
            var tl = tile + new Vector2i(0, 1);

            if (!tiles.Contains(tile + Vector2i.Down))
                AddEdge(tile, br);

            if (!tiles.Contains(tile + Vector2i.Right))
                AddEdge(br, tr);

            if (!tiles.Contains(tile + Vector2i.Up))
                AddEdge(tr, tl);

            if (!tiles.Contains(tile + Vector2i.Left))
                AddEdge(tl, tile);
        }

        var totalEdges = outgoing.Values.Sum(l => l.Count);
        var edgesLeft = new Dictionary<Vector2i, int>();

        foreach (var (corner, list) in outgoing)
        {
            edgesLeft[corner] = list.Count;
        }

        var loops = new List<List<Vector2i>>();

        foreach (var start in outgoing.Keys.ToList())
        {
            // A pinch corner can be the start of more than one loop - keep draining its
            // remaining edges instead of stopping after the first one found here.
            while (edgesLeft[start] > 0)
            {
                var first = TakeEdge(start, default);

                if (first is not { } firstCorner)
                    break;

                var raw = new List<Vector2i> { start };
                var cur = firstCorner;
                var dirIn = firstCorner - start;
                var closed = false;

                // Bounded by the total edge supply: every iteration consumes one real
                // edge, so this can never spin forever even on malformed input.
                for (var steps = 0; steps <= totalEdges; steps++)
                {
                    if (cur == start)
                    {
                        closed = true;
                        break;
                    }

                    raw.Add(cur);
                    var next = TakeEdge(cur, dirIn);

                    if (next is not { } nextCorner)
                        break;

                    dirIn = nextCorner - cur;
                    cur = nextCorner;
                }

                if (!closed)
                    continue; // broken/degenerate chain - discard and try the next one

                var loop = new List<Vector2i>();

                for (var i = 0; i < raw.Count; i++)
                {
                    var prev = raw[(i - 1 + raw.Count) % raw.Count];
                    var point = raw[i];
                    var nextPoint = raw[(i + 1) % raw.Count];

                    if (point - prev != nextPoint - point)
                        loop.Add(point);
                }

                loops.Add(loop);
            }
        }

        return loops;

        void AddEdge(Vector2i from, Vector2i to)
        {
            if (!outgoing.TryGetValue(from, out var list))
                outgoing[from] = list = new List<Vector2i>();

            list.Add(to);
        }

        // Picks and consumes one still-available outgoing edge from 'from'. When there's
        // only one candidate it's used unconditionally; when there are two (a pinch
        // corner), the one forming a left turn relative to dirIn wins - see remarks above.
        Vector2i? TakeEdge(Vector2i from, Vector2i dirIn)
        {
            if (!outgoing.TryGetValue(from, out var candidates) || edgesLeft[from] <= 0)
                return null;

            var offset = candidates.Count - edgesLeft[from];
            var bestOffset = 0;

            if (edgesLeft[from] > 1)
            {
                var bestCross = int.MinValue;

                for (var i = 0; i < edgesLeft[from]; i++)
                {
                    var dir = candidates[offset + i] - from;
                    var cross = dirIn.X * dir.Y - dirIn.Y * dir.X;

                    if (cross > bestCross)
                    {
                        bestCross = cross;
                        bestOffset = i;
                    }
                }
            }

            var index = offset + bestOffset;
            (candidates[offset], candidates[index]) = (candidates[index], candidates[offset]);
            edgesLeft[from]--;
            return candidates[offset];
        }
    }

    private static MapCoordinates GridCornerToMap(EntityUid gridUid, Vector2i corner, TransformSystem transform)
        => transform.ToMapCoordinates(new EntityCoordinates(gridUid, corner));

    private static void DrawDashedSegment(
        in OverlayDrawArgs args,
        MapCoordinates start,
        MapCoordinates end,
        Color color,
        ShaderPrototype shaderId)
    {
        var shader = shaderId.InstanceUnique();
        var prevShader = args.WorldHandle.GetShader();

        var screenA = args.Viewport.WorldToLocal(start.Position);
        screenA.Y = args.Viewport.Size.Y - screenA.Y;
        var screenB = args.Viewport.WorldToLocal(end.Position);
        screenB.Y = args.Viewport.Size.Y - screenB.Y;

        shader.SetParameter("color", color);
        shader.SetParameter("point1", screenA);
        shader.SetParameter("point2", screenB);

        var area = new Box2(
            Math.Min(start.X, end.X),
            Math.Min(start.Y, end.Y),
            Math.Max(start.X, end.X),
            Math.Max(start.Y, end.Y)).Enlarged(0.15f);

        args.WorldHandle.UseShader(shader);
        args.WorldHandle.DrawRect(area, Color.White);
        args.WorldHandle.UseShader(prevShader);
    }

    #endregion
}
