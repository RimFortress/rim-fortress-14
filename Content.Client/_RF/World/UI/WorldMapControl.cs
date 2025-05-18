using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Shared._RF.Parallax.Fog;
using Content.Shared._RF.Pinpointer;
using Content.Shared.Maps;
using Content.Shared.Parallax.Biomes;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Collections;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Client._RF.World.UI;

public sealed class WorldMapControl : MapGridControl
{
    private const byte ChunkSize = SharedBiomeSystem.ChunkSize;
    private const float UpdateTime = 1.0f;
    private const float MinDisplayedRange = 8f;
    private const float MaxDisplayedRange = 128f;
    private const float DefaultDisplayedRange = 48f;

    public EntityUid? MapUid;
    public Color FogColor = Color.Black.WithAlpha(0.3f);

    private float _updateTimer = 1.0f;

    private readonly Dictionary<Vector2i, Color> _tiles = new();
    private readonly Dictionary<Color, List<(Vector2, Vector2)>> _lines = new();
    private readonly Dictionary<Color, List<Vector2>> _circles = new();
    private readonly HashSet<Vector2i> _chunks = new();

    protected override bool Draggable => true;

    public WorldMapControl() : base(MinDisplayedRange, MaxDisplayedRange, DefaultDisplayedRange)
    {
    }

    public void CenterToCoordinates(Vector2 coordinates)
    {
        Offset = coordinates;
    }

    private void UpdateTiles()
    {
        if (MapUid == null
            || !EntManager.TryGetComponent(MapUid.Value, out MapGridComponent? grid)
            || !EntManager.TryGetComponent(MapUid.Value, out FogOfWarComponent? fog)
            || !EntManager.TryGetComponent(fog.FowGrid, out MapGridComponent? fawGrid))
            return;

        var map = EntManager.System<MapSystem>();
        var ent = new Entity<MapGridComponent>(MapUid.Value, grid);
        var fawEnt = new Entity<MapGridComponent>(fog.FowGrid, fawGrid);

        foreach (var chunk in fog.ActiveChunks)
        {
            for (var x = 0; x < ChunkSize; x++)
            {
                for (var y = 0; y < ChunkSize; y++)
                {
                    var indices = new Vector2i(x + chunk.X, y + chunk.Y);
                    var color = map.GetTileRef(ent, indices).GetContentTileDefinition().NavMapColor;

                    _tiles[indices] = color;
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
                }
            }

            _chunks.Add(chunk);
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
                _lines[color].Add((point + new Vector2(0f, 0f), point + new Vector2(1f, 0f)));

            if (!points.ContainsKey(point + new Vector2i(1, 0)))
                _lines[color].Add((point + new Vector2(1f, 1f), point + new Vector2(1f, 0f)));

            if (!points.ContainsKey(point + new Vector2i(-1, 0)))
                _lines[color].Add((point + new Vector2(0f, 1f), point + new Vector2(0f, 0f)));
        }
    }

    public void UpdateMap()
    {
        _tiles.Clear();
        _lines.Clear();
        _chunks.Clear();

        UpdateTiles();
        UpdateStructs();
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

        foreach (var (tile, color) in _tiles)
        {
            var box = ScaleOffsetBox(tile, new Vector2(1f, 1f));
            handle.DrawRect(box, color);
        }

        foreach (var (color, points) in _lines)
        {
            var verts = new ValueList<Vector2>(points.Count * 2);
            foreach (var (start, end) in points)
            {
                verts.Add(ScaleOffsetPos(start));
                verts.Add(ScaleOffsetPos(end));
            }

            handle.DrawPrimitives(DrawPrimitiveTopology.LineList, verts.Span, color);
        }

        foreach (var (color, points) in _circles)
        {
            foreach (var point in points)
            {
                handle.DrawCircle(ScaleOffsetPos(point), 0.4f * MinimapScale, color);
            }
        }

        foreach (var chunk in _chunks)
        {
            var box = ScaleOffsetBox(chunk, new Vector2(ChunkSize));
            handle.DrawRect(box, FogColor);
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
}

