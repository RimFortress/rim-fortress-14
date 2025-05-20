using System.Linq;
using System.Numerics;
using Content.Client.Light.EntitySystems;
using Content.Client.UserInterface.Controls;
using Content.Shared._RF.Parallax.Fog;
using Content.Shared._RF.Pinpointer;
using Content.Shared._RF.World;
using Content.Shared.Light.Components;
using Content.Shared.Maps;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Pinpointer;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Collections;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Client._RF.World.UI;

public sealed class WorldMapControl : MapGridControl
{
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IResourceCache _cache = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    private const byte ChunkSize = SharedBiomeSystem.ChunkSize;
    private const float UpdateTime = 2.0f;
    private const float MinDisplayedRange = 8f;
    private const float MaxDisplayedRange = 128f;
    private const float DefaultDisplayedRange = 48f;

    public EntityUid? MapUid;
    public Color FogColor = Color.Black.WithAlpha(0.5f);
    public Color RoofColor = Color.Black;

    private float _updateTimer = 1.0f;

    private readonly Font _font;

    private readonly Dictionary<Vector2i, Color> _tiles = new();
    private readonly HashSet<Vector2i> _roofs = new();
    private readonly Dictionary<Color, List<(Vector2, Vector2)>> _lines = new();
    private readonly Dictionary<Color, List<Vector2>> _circles = new();
    private readonly Dictionary<Color, List<Vector2>> _noScaleCircles = new();
    private readonly Dictionary<Color, HashSet<UIBox2>> _chunks = new();
    private readonly Dictionary<Color, HashSet<UIBox2>> _regions = new();
    private readonly Dictionary<Color, List<(Vector2, string)>> _noScaleStrings = new();

    protected override bool Draggable => true;

    public WorldMapControl() : base(MinDisplayedRange, MaxDisplayedRange, DefaultDisplayedRange)
    {
        _font = new VectorFont(_cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf"), 10);
    }

    public void CenterToCoordinates(Vector2 coordinates)
    {
        Offset = coordinates;
    }

    public Vector2i MousePos()
    {
        if (_input.MouseScreenPosition is not { IsValid: true } screen
            || !GlobalPixelRect.Contains((Vector2i) screen.Position))
            return Vector2i.Zero;

        var pos = screen.Position / UIScale - GlobalPosition;
        var a = (pos - MidPointVector) / MinimapScale;
        var coords = new Vector2(a.X, -a.Y) + Offset;

        return (Vector2i) coords;
    }

    private void UpdateTiles()
    {
        if (MapUid == null
            || !EntManager.TryGetComponent(MapUid.Value, out MapGridComponent? grid)
            || !EntManager.TryGetComponent(MapUid.Value, out FogOfWarComponent? fog)
            || !EntManager.TryGetComponent(MapUid.Value, out RoofComponent? roof)
            || !EntManager.TryGetComponent(fog.FowGrid, out MapGridComponent? fawGrid)
            || !EntManager.TryGetComponent(fog.FowGrid, out RoofComponent? fawRoof))
            return;

        var map = EntManager.System<MapSystem>();
        var roofSys = EntManager.System<RoofSystem>();

        var ent = new Entity<MapGridComponent>(MapUid.Value, grid);
        var roofEnt = new Entity<MapGridComponent, RoofComponent>(MapUid.Value, grid, roof);

        var fawEnt = new Entity<MapGridComponent>(fog.FowGrid, fawGrid);
        var fawRoofEnt = new Entity<MapGridComponent, RoofComponent>(fog.FowGrid, fawGrid, fawRoof);

        foreach (var chunk in fog.ActiveChunks)
        {
            for (var x = 0; x < ChunkSize; x++)
            {
                for (var y = 0; y < ChunkSize; y++)
                {
                    var indices = new Vector2i(x + chunk.X, y + chunk.Y);
                    var color = map.GetTileRef(ent, indices).GetContentTileDefinition().NavMapColor;

                    _tiles[indices] = color;

                    if (roofSys.IsRooved(roofEnt, indices))
                        _roofs.Add(indices);
                }
            }
        }

        foreach (var chunk in fog.FogChunks)
        {
            for (var x = 0; x < ChunkSize; x++)
            {
                for (var y = 0; y < ChunkSize; y++)
                {
                    var indices = new Vector2i(x + chunk.X, y + chunk.Y);
                    var color = map.GetTileRef(fawEnt, indices).GetContentTileDefinition().NavMapColor;

                    _tiles[indices] = color;

                    if (roofSys.IsRooved(fawRoofEnt, indices) || roofSys.IsRooved(roofEnt, indices))
                        _roofs.Add(indices);
                }
            }

            _chunks.TryAdd(FogColor, new());
            _chunks[FogColor].Add(new UIBox2(chunk.X, chunk.Y + ChunkSize, chunk.X + ChunkSize, chunk.Y));
        }
    }

    private void ApplyRoof()
    {
        var roofs = _roofs.ToList();
        var directions = new Vector2i[]
        {
            new(0, 1), new(1, 1), new(1, 0), new(1, -1), new(0, -1), new(-1, -1), new(-1, 0), new(-1, 1),
        };

        foreach (var roof in roofs)
        {
            foreach (var dir in directions)
            {
                if (roofs.Contains(roof + dir) || !_tiles.ContainsKey(roof + dir))
                    continue;

                _roofs.Remove(roof);
            }
        }

        foreach (var roof in _roofs)
        {
            _tiles[roof] = RoofColor;
        }
    }

    private void UpdateStructs()
    {
        if (MapUid == null || !EntManager.TryGetComponent(MapUid.Value, out MapGridComponent? grid))
            return;

        var map = EntManager.System<MapSystem>();
        var entities = EntManager.EntityQueryEnumerator<WorldMapStructComponent, TransformComponent>();
        var points = new Dictionary<Vector2i, Color>();

        while (entities.MoveNext(out var comp, out var xform))
        {
            if (xform.MapUid != MapUid)
                continue;

            var tileRef = map.GetTileRef(MapUid.Value, grid, xform.Coordinates);

            if (_roofs.Contains(tileRef.GridIndices) || !_tiles.ContainsKey(tileRef.GridIndices))
                continue;

            switch (comp.DrawType)
            {
                case WorldMapChunkType.Wall:
                    _tiles[tileRef.GridIndices] = comp.MainColor;
                    points[tileRef.GridIndices] = comp.SecondaryColor;
                    break;
                case WorldMapChunkType.Tile:
                    _tiles[tileRef.GridIndices] = comp.MainColor;
                    break;
                case WorldMapChunkType.Dot:
                    _circles.TryAdd(comp.MainColor, new());
                    _circles[comp.MainColor].Add(tileRef.GridIndices + new Vector2(0.5f));
                    break;
            }
        }

        foreach (var (point, color) in points)
        {
            _lines.TryAdd(color, new());

            if (!points.ContainsKey(point + new Vector2i(0, 1)))
                _lines[color].Add((point + new Vector2(0, 1f), point + new Vector2(1f, 1f)));

            if (!points.ContainsKey(point + new Vector2i(0, -1)))
                _lines[color].Add((point, point + new Vector2(1f, 0f)));

            if (!points.ContainsKey(point + new Vector2i(1, 0)))
                _lines[color].Add((point + new Vector2(1f, 1f), point + new Vector2(1f, 0f)));

            if (!points.ContainsKey(point + new Vector2i(-1, 0)))
                _lines[color].Add((point + new Vector2(0f, 1f), point));
        }
    }

    private void UpdateBeacons()
    {
        if (MapUid == null || !EntManager.TryGetComponent(MapUid.Value, out MapGridComponent? grid))
            return;

        var map = EntManager.System<MapSystem>();

        if (_player.LocalSession is { } session
            && EntManager.TryGetComponent(_player.LocalEntity, out TransformComponent? playerForm)
            && EntManager.TryGetComponent(_player.LocalEntity, out RimFortressPlayerComponent? player))
        {
            var tileRef = map.GetTileRef(MapUid.Value, grid, playerForm.Coordinates);

            _noScaleCircles.TryAdd(player.FactionColor, new());
            _noScaleStrings.TryAdd(player.FactionColor, new());

            _noScaleCircles[player.FactionColor].Add(tileRef.GridIndices + new Vector2(0.5f));
            _noScaleStrings[player.FactionColor].Add((tileRef.GridIndices + new Vector2(1.5f), session.Data.UserName));
        }

        var entities = EntManager.EntityQueryEnumerator<NavMapBeaconComponent, TransformComponent>();
        while (entities.MoveNext(out var comp, out var xform))
        {
            if (comp.Text == null)
                continue;

            var tileRef = map.GetTileRef(MapUid.Value, grid, xform.Coordinates);

            if (!_tiles.ContainsKey(tileRef.GridIndices))
                continue;

            _noScaleCircles.TryAdd(comp.Color, new());
            _noScaleStrings.TryAdd(comp.Color, new());

            _noScaleCircles[comp.Color].Add(tileRef.GridIndices + new Vector2(0.5f));
            _noScaleStrings[comp.Color].Add((tileRef.GridIndices + new Vector2(1.5f), comp.Text));
        }
    }

    public void UpdateMap()
    {
        _tiles.Clear();
        _lines.Clear();
        _chunks.Clear();
        _regions.Clear();
        _roofs.Clear();
        _circles.Clear();
        _noScaleCircles.Clear();
        _noScaleStrings.Clear();

        UpdateTiles();
        ApplyRoof();
        UpdateStructs();
        UpdateBeacons();
        MergeTileRegions();

        _tiles.Clear();
        _roofs.Clear();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        _updateTimer += args.DeltaSeconds;

        if (_updateTimer < UpdateTime)
            return;

        _updateTimer = 0;
        UpdateMap();
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        var viewBox = (UIBox2) PixelRect.Translated(-PixelPosition);

        foreach (var (color, regions) in _regions)
        {
            foreach (var region in regions)
            {
                var box = ScaleOffsetBox(region);

                if (!viewBox.Intersects(box))
                    continue;

                handle.DrawRect(box, color);
            }
        }

        foreach (var (color, points) in _lines)
        {
            var verts = new ValueList<Vector2>(points.Count * 2);
            foreach (var (start, end) in points)
            {
                var s = ScaleOffsetPos(start);
                var e = ScaleOffsetPos(end);

                if (!viewBox.Contains(s) && !viewBox.Contains(e))
                    continue;

                verts.Add(s);
                verts.Add(e);
            }

            handle.DrawPrimitives(DrawPrimitiveTopology.LineList, verts.Span, color);
        }

        foreach (var (color, points) in _circles)
        {
            foreach (var point in points)
            {
                var pos = ScaleOffsetPos(point);

                if (!viewBox.Contains(pos))
                    continue;

                handle.DrawCircle(pos, 0.4f * MinimapScale, color);
            }
        }

        foreach (var (color, chunks) in _chunks)
        {
            foreach (var chunk in chunks)
            {
                var box = ScaleOffsetBox(chunk);

                if (!viewBox.Intersects(box))
                    continue;

                handle.DrawRect(box, color);
            }
        }

        foreach (var (color, points) in _noScaleCircles)
        {
            foreach (var point in points)
            {
                var pos = ScaleOffsetPos(point);

                if (!viewBox.Contains(pos))
                    continue;

                handle.DrawCircle(pos, 5, color);
            }
        }

        foreach (var (color, points) in _noScaleStrings)
        {
            foreach (var (point, text) in points)
            {
                var pos = ScaleOffsetPos(point);

                if (!viewBox.Contains(pos))
                    continue;

                handle.DrawString(_font, pos, text, color);
            }
        }
    }

    private Vector2 ScaleOffsetPos(Vector2 pos)
    {
        return ScalePosition(new Vector2(pos.X - Offset.X, (pos.Y - Offset.Y) * -1));
    }

    private UIBox2 ScaleOffsetBox(Vector2 leftBottom, Vector2 size)
    {
        var lt = ScaleOffsetPos(new Vector2(leftBottom.X, leftBottom.Y + size.Y));
        var rb = ScaleOffsetPos(new Vector2(leftBottom.X + size.X, leftBottom.Y));
        return new UIBox2(lt, rb);
    }

    private UIBox2 ScaleOffsetBox(UIBox2 box)
    {
        return ScaleOffsetBox(box.BottomLeft, box.Size);
    }

    private void MergeTileRegions()
    {
        var tiles = new HashSet<Vector2i>(_tiles.Keys);

        while (tiles.Count > 0)
        {
            var startTile = tiles.First();
            var color = _tiles[startTile];
            var region = FindMaxRectangle(startTile, color);

            _regions.TryAdd(color, new());
            _regions[color].Add(region);

            for (var x = (int) region.Left; x < region.Right; x++)
            {
                for (var y = (int) region.Bottom; y < region.Top; y++)
                {
                    tiles.Remove(new Vector2i(x, y));
                }
            }
        }
    }

    private UIBox2 FindMaxRectangle(Vector2i startTile, Color color)
    {
        var left = startTile.X;
        var top = startTile.Y;
        var right = startTile.X;
        var bottom = startTile.Y;

        while (true)
        {
            var expandLeft = true;
            for (var i = bottom; i <= top; i++)
            {
                if (_tiles.TryGetValue(new Vector2i(left - 1, i), out var col) && col == color)
                    continue;

                expandLeft = false;
                break;
            }

            if (expandLeft)
                left--;

            var expandRight = true;
            for (var i = bottom; i <= top; i++)
            {
                if (_tiles.TryGetValue(new Vector2i(right + 1, i), out var col) && col == color)
                    continue;

                expandRight = false;
                break;
            }

            if (expandRight)
                right++;

            var expandTop = true;
            for (var i = left; i <= right; i++)
            {
                if (_tiles.TryGetValue(new Vector2i(i, top + 1), out var col) && col == color)
                    continue;

                expandTop = false;
                break;
            }

            if (expandTop)
                top++;

            var expandBottom = true;
            for (var i = left; i <= right; i++)
            {
                if (_tiles.TryGetValue(new Vector2i(i, bottom - 1), out var col) && col == color)
                    continue;

                expandBottom = false;
                break;
            }

            if (expandBottom)
                bottom--;

            if (!expandLeft && !expandRight && !expandTop && !expandBottom)
                break;
        }

        return new UIBox2(left, top + 1f, right + 1f, bottom);
    }
}

