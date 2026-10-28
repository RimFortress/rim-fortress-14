using Content.Shared._RF.CCVar;
using Content.Shared._RF.Parallax.Fog;
using Content.Shared._RF.Pinpointer;
using Content.Shared.Light.Components;
using Content.Shared.Maps;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Pinpointer;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Graphics;
using Robust.Shared.Map.Components;

namespace Content.Client._RF.World;

/// <summary>
/// Keeps the world map as baked textures, one per chunk. A chunk is baked again only when something in it changes:
/// tiles, roofs and map structures notify about it through events, nothing is polled by time.
/// </summary>
public sealed partial class WorldMapChunkSystem : EntitySystem
{
    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private EntityQuery<TransformComponent> _xformQuery;
    [Dependency] private EntityQuery<MapGridComponent> _gridQuery;
    [Dependency] private EntityQuery<WorldMapStructComponent> _structQuery;

    public const int ChunkSize = SharedBiomeSystem.ChunkSize;

    /// <summary>
    /// Pixels of the chunk texture per tile
    /// </summary>
    public const int PixelsPerTile = 8;

    /// <summary>
    /// Tiles around the chunk that are needed to bake it: the roof edges and the outlines of walls depend on neighbors
    /// </summary>
    private const int Margin = 2;
    private const int Region = ChunkSize + Margin * 2;

    private static readonly Color RoofColor = Color.Black;

    private static readonly Vector2i[] Neighbors =
    [
        new(0, 1), new(1, 1), new(1, 0), new(1, -1), new(0, -1), new(-1, -1), new(-1, 0), new(-1, 1),
    ];

    /// <summary>
    /// The map that is baked right now, null if nobody looks at the world map
    /// </summary>
    public EntityUid? Map { get; private set; }

    private readonly Dictionary<Vector2i, IRenderTexture> _chunks = new();
    private readonly HashSet<Vector2i> _dirty = new();

    private readonly HashSet<EntityUid> _beacons = new();

    // Entities are placed on the tiles lazily: on startup of the component the entity may have no position yet
    private readonly HashSet<EntityUid> _pendingStructs = new();
    private readonly Dictionary<EntityUid, Vector2i> _structTile = new();
    private readonly Dictionary<Vector2i, List<EntityUid>> _structsByTile = new();

    private readonly HashSet<EntityUid> _pendingRoofs = new();
    private readonly Dictionary<EntityUid, Vector2i> _roofTile = new();
    private readonly Dictionary<Vector2i, int> _roofTiles = new();

    private Dictionary<Vector2i, ulong> _roofData = new();

    private readonly List<EntityUid> _scratch = new();
    private readonly List<Vector2i> _bakeList = new();
    private readonly List<(UIBox2 Rect, Color Color)> _rects = new();

    private readonly Color?[] _colors = new Color?[Region * Region];
    private readonly bool[] _roofs = new bool[Region * Region];
    private readonly Color?[] _final = new Color?[Region * Region];
    private readonly Color?[] _walls = new Color?[Region * Region];
    private readonly Color?[] _dots = new Color?[Region * Region];

    public IReadOnlyCollection<EntityUid> Beacons => _beacons;

    public override void Shutdown()
    {
        base.Shutdown();

        Detach();
    }

    #region Public API

    /// <summary>
    /// Starts tracking the map. The baked chunks stay while the map exists, the window only draws them.
    /// </summary>
    [PublicAPI]
    public void Attach(EntityUid? map)
    {
        if (Map == map)
            return;

        Detach();

        if (map == null)
            return;

        Map = map;

        if (TryComp(map, out RoofComponent? roof))
            _roofData = new Dictionary<Vector2i, ulong>(roof.Data);

        var structs = EntityQueryEnumerator<WorldMapStructComponent>();
        while (structs.MoveNext(out var uid, out _))
        {
            _pendingStructs.Add(uid);
        }

        var roofs = EntityQueryEnumerator<IsRoofComponent>();
        while (roofs.MoveNext(out var uid, out _))
        {
            _pendingRoofs.Add(uid);
        }

        MarkGridDirty(map.Value);

        if (TryComp(map, out FogOfWarComponent? fog))
            MarkGridDirty(fog.FowGrid);
    }

    /// <summary>
    /// Stops tracking and frees the textures
    /// </summary>
    [PublicAPI]
    public void Detach()
    {
        Map = null;

        foreach (var texture in _chunks.Values)
        {
            texture.Dispose();
        }

        _chunks.Clear();
        _dirty.Clear();
        _pendingStructs.Clear();
        _structTile.Clear();
        _structsByTile.Clear();
        _pendingRoofs.Clear();
        _roofTile.Clear();
        _roofTiles.Clear();
        _roofData = new();
    }

    [PublicAPI]
    public bool TryGetChunk(Vector2i chunk, out Texture texture)
    {
        if (_chunks.TryGetValue(chunk, out var target))
        {
            texture = target.Texture;
            return true;
        }

        texture = default!;
        return false;
    }

    [PublicAPI]
    public bool HasChunk(Vector2i chunk) => _chunks.ContainsKey(chunk);

    [PublicAPI]
    public static Vector2i ToChunk(Vector2i tile) => SharedMapSystem.GetChunkIndices(tile, ChunkSize);

    #endregion

    #region Events

    [SubscribeLocalEvent]
    private void OnFogInit(Entity<FogOfWarComponent> ent, ref ComponentInit args)
    {
        // The chunks are kept while the window is closed, so they are ready the next time it opens
        if (Map == null)
            Attach(ent);
    }

    [SubscribeLocalEvent]
    private void OnTileChanged(ref TileChangedEvent args)
    {
        if (Map is not { } map)
            return;

        var grid = args.Entity.Owner;

        if (grid != map && !(TryComp(map, out FogOfWarComponent? fog) && fog.FowGrid == grid))
            return;

        foreach (var change in args.Changes)
        {
            // The chunk index in the change can't be trusted, see FogOfWarSystem.SetTilesSafe
            MarkTileDirty(change.GridIndices);
        }
    }

    [SubscribeLocalEvent]
    private void OnRoofState(Entity<RoofComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (Map != ent.Owner)
            return;

        foreach (var (index, mask) in ent.Comp.Data)
        {
            if (!_roofData.TryGetValue(index, out var old) || old != mask)
                MarkRoofChunkDirty(index);
        }

        foreach (var index in _roofData.Keys)
        {
            if (!ent.Comp.Data.ContainsKey(index))
                MarkRoofChunkDirty(index);
        }

        _roofData = new Dictionary<Vector2i, ulong>(ent.Comp.Data);
    }

    [SubscribeLocalEvent]
    private void OnBeaconStartup(Entity<NavMapBeaconComponent> ent, ref ComponentStartup args)
    {
        _beacons.Add(ent);
    }

    [SubscribeLocalEvent]
    private void OnBeaconShutdown(Entity<NavMapBeaconComponent> ent, ref ComponentShutdown args)
    {
        _beacons.Remove(ent);
    }

    [SubscribeLocalEvent]
    private void OnStructStartup(Entity<WorldMapStructComponent> ent, ref ComponentStartup args)
    {
        if (Map != null)
            _pendingStructs.Add(ent);
    }

    [SubscribeLocalEvent]
    private void OnStructShutdown(Entity<WorldMapStructComponent> ent, ref ComponentShutdown args)
    {
        _pendingStructs.Remove(ent);
        RemoveStruct(ent);
    }

    [SubscribeLocalEvent]
    private void OnIsRoofStartup(Entity<IsRoofComponent> ent, ref ComponentStartup args)
    {
        if (Map != null)
            _pendingRoofs.Add(ent);
    }

    [SubscribeLocalEvent]
    private void OnIsRoofShutdown(Entity<IsRoofComponent> ent, ref ComponentShutdown args)
    {
        _pendingRoofs.Remove(ent);
        RemoveRoof(ent);
    }

    [SubscribeLocalEvent]
    private void OnIsRoofState(Entity<IsRoofComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (Map == null)
            return;

        RemoveRoof(ent);
        _pendingRoofs.Add(ent);
    }

    private void RemoveStruct(EntityUid uid)
    {
        if (!_structTile.Remove(uid, out var tile))
            return;

        if (_structsByTile.TryGetValue(tile, out var list))
        {
            list.Remove(uid);

            if (list.Count == 0)
                _structsByTile.Remove(tile);
        }

        MarkTileDirty(tile);
    }

    private void RemoveRoof(EntityUid uid)
    {
        if (!_roofTile.Remove(uid, out var tile))
            return;

        if (_roofTiles.TryGetValue(tile, out var count))
        {
            if (count <= 1)
                _roofTiles.Remove(tile);
            else
                _roofTiles[tile] = count - 1;
        }

        MarkTileDirty(tile);
    }

    #endregion

    #region Dirty

    private void MarkGridDirty(EntityUid grid)
    {
        if (!_gridQuery.TryComp(grid, out var comp))
            return;

        var last = new Vector2i(int.MinValue, int.MinValue);
        var tiles = _map.GetAllTiles(grid, comp);

        while (tiles.MoveNext(out var tile))
        {
            var chunk = ToChunk(tile.Value.GridIndices);

            if (chunk == last)
                continue;

            last = chunk;
            _dirty.Add(chunk);
        }
    }

    private void MarkTileDirty(Vector2i tile)
    {
        var chunk = ToChunk(tile);
        var local = tile - chunk * ChunkSize;

        _dirty.Add(chunk);

        // The edge of the roof and the outlines of walls look at the neighbors
        var left = local.X < Margin;
        var right = local.X >= ChunkSize - Margin;
        var down = local.Y < Margin;
        var up = local.Y >= ChunkSize - Margin;

        if (left)
            _dirty.Add(chunk + new Vector2i(-1, 0));

        if (right)
            _dirty.Add(chunk + new Vector2i(1, 0));

        if (down)
            _dirty.Add(chunk + new Vector2i(0, -1));

        if (up)
            _dirty.Add(chunk + new Vector2i(0, 1));

        if (left && down)
            _dirty.Add(chunk + new Vector2i(-1, -1));

        if (left && up)
            _dirty.Add(chunk + new Vector2i(-1, 1));

        if (right && down)
            _dirty.Add(chunk + new Vector2i(1, -1));

        if (right && up)
            _dirty.Add(chunk + new Vector2i(1, 1));
    }

    private void MarkRoofChunkDirty(Vector2i roofChunk)
    {
        var origin = roofChunk * RoofComponent.ChunkSize;
        var max = RoofComponent.ChunkSize - 1;

        MarkTileDirty(origin);
        MarkTileDirty(origin + new Vector2i(max, 0));
        MarkTileDirty(origin + new Vector2i(0, max));
        MarkTileDirty(origin + new Vector2i(max, max));
    }

    #endregion

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (Map is not { } map)
            return;

        if (!Exists(map) || LifeStage(map) >= EntityLifeStage.Terminating)
        {
            Detach();
            return;
        }

        if (!_gridQuery.TryComp(map, out var grid))
            return;

        PlacePending(map, grid);
    }

    private void PlacePending(EntityUid map, MapGridComponent grid)
    {
        if (_pendingStructs.Count > 0)
        {
            _scratch.Clear();
            _scratch.AddRange(_pendingStructs);

            foreach (var uid in _scratch)
            {
                if (!TryGetTile(uid, map, grid, out var tile, out var keep))
                {
                    if (!keep)
                        _pendingStructs.Remove(uid);

                    continue;
                }

                _pendingStructs.Remove(uid);
                RemoveStruct(uid);

                _structTile[uid] = tile;

                if (!_structsByTile.TryGetValue(tile, out var list))
                    _structsByTile[tile] = list = new();

                list.Add(uid);
                MarkTileDirty(tile);
            }
        }

        if (_pendingRoofs.Count > 0)
        {
            _scratch.Clear();
            _scratch.AddRange(_pendingRoofs);

            foreach (var uid in _scratch)
            {
                if (!TryGetTile(uid, map, grid, out var tile, out var keep))
                {
                    if (!keep)
                        _pendingRoofs.Remove(uid);

                    continue;
                }

                _pendingRoofs.Remove(uid);
                RemoveRoof(uid);

                if (TryComp(uid, out IsRoofComponent? roof) && !roof.Enabled)
                    continue;

                _roofTile[uid] = tile;
                _roofTiles[tile] = _roofTiles.GetValueOrDefault(tile) + 1;
                MarkTileDirty(tile);
            }
        }
    }

    /// <param name="keep">Whether the entity should be tried again later</param>
    private bool TryGetTile(EntityUid uid, EntityUid map, MapGridComponent grid, out Vector2i tile, out bool keep)
    {
        tile = default;
        keep = false;

        if (!Exists(uid) || !_xformQuery.TryComp(uid, out var xform))
            return false;

        if (xform.MapUid != map)
        {
            // Without a map the entity is probably still being set up
            keep = xform.MapUid == null;
            return false;
        }

        tile = _map.WorldToTile(map, grid, _transform.GetWorldPosition(xform));
        return true;
    }

    #region Baking

    private static int Index(int x, int y) => y * Region + x;

    /// <summary>
    /// Draws the changed chunks into their textures. Should be called while the world map is drawn, because only then
    /// there is a handle to draw into the render targets.
    /// </summary>
    public void Bake(DrawingHandleScreen handle)
    {
        if (Map is not { } map || _dirty.Count == 0 || !_gridQuery.TryComp(map, out var grid))
            return;

        var budget = Math.Max(1, _cfg.GetCVar(RfVars.WorldMapChunksPerFrame));

        _bakeList.Clear();
        foreach (var chunk in _dirty)
        {
            _bakeList.Add(chunk);

            if (_bakeList.Count >= budget)
                break;
        }

        foreach (var chunk in _bakeList)
        {
            _dirty.Remove(chunk);
            Bake(handle, (map, grid), chunk);
        }
    }

    private void Bake(DrawingHandleScreen handle, Entity<MapGridComponent> map, Vector2i chunk)
    {
        var fowGrid = TryComp(map, out FogOfWarComponent? fog) && _gridQuery.TryComp(fog.FowGrid, out var fow)
            ? new Entity<MapGridComponent>(fog.FowGrid, fow)
            : (Entity<MapGridComponent>?) null;

        TryComp(map, out RoofComponent? roof);

        // The tile at (x, y) of the region is origin + (x - Margin, y - Margin)
        var origin = chunk * ChunkSize - new Vector2i(Margin, Margin);

        for (var x = 0; x < Region; x++)
        {
            for (var y = 0; y < Region; y++)
            {
                var i = Index(x, y);
                var tile = origin + new Vector2i(x, y);

                _colors[i] = GetTileColor(map, fowGrid, tile);
                _roofs[i] = _colors[i] != null && IsRoof(roof, tile);
                _final[i] = null;
                _walls[i] = null;
                _dots[i] = null;
            }
        }

        // The edge of the roof is not painted, so the border of the area under it is seen
        for (var x = 1; x < Region - 1; x++)
        {
            for (var y = 1; y < Region - 1; y++)
            {
                var i = Index(x, y);

                if (_colors[i] == null)
                    continue;

                var roofed = _roofs[i];

                if (roofed)
                {
                    foreach (var dir in Neighbors)
                    {
                        var n = Index(x + dir.X, y + dir.Y);

                        if (_colors[n] == null || _roofs[n])
                            continue;

                        roofed = false;
                        break;
                    }
                }

                if (roofed)
                {
                    _final[i] = RoofColor;
                    continue;
                }

                _final[i] = _colors[i];

                if (!_structsByTile.TryGetValue(origin + new Vector2i(x, y), out var structs))
                    continue;

                foreach (var uid in structs)
                {
                    if (!_structQuery.TryComp(uid, out var comp))
                        continue;

                    switch (comp.DrawType)
                    {
                        case WorldMapChunkType.Wall:
                            _final[i] = comp.MainColor;
                            _walls[i] = comp.SecondaryColor;
                            break;
                        case WorldMapChunkType.Tile:
                            _final[i] = comp.MainColor;
                            break;
                        case WorldMapChunkType.Dot:
                            _dots[i] = comp.MainColor;
                            break;
                    }
                }
            }
        }

        var any = false;
        _rects.Clear();

        for (var lx = 0; lx < ChunkSize; lx++)
        {
            for (var ly = 0; ly < ChunkSize; ly++)
            {
                var x = lx + Margin;
                var y = ly + Margin;
                var i = Index(x, y);

                if (_final[i] is not { } color)
                    continue;

                any = true;

                // The target goes from the top to the bottom, and the world goes from the bottom to the top
                var px = lx * PixelsPerTile;
                var py = (ChunkSize - 1 - ly) * PixelsPerTile;

                _rects.Add((Pixels(px, py, PixelsPerTile, PixelsPerTile), color));

                if (_walls[i] is { } outline)
                {
                    if (_walls[Index(x - 1, y)] == null)
                        _rects.Add((Pixels(px, py, 1, PixelsPerTile), outline));

                    if (_walls[Index(x + 1, y)] == null)
                        _rects.Add((Pixels(px + PixelsPerTile - 1, py, 1, PixelsPerTile), outline));

                    if (_walls[Index(x, y + 1)] == null)
                        _rects.Add((Pixels(px, py, PixelsPerTile, 1), outline));

                    if (_walls[Index(x, y - 1)] == null)
                        _rects.Add((Pixels(px, py + PixelsPerTile - 1, PixelsPerTile, 1), outline));
                }

                if (_dots[i] is { } dot)
                {
                    var offset = PixelsPerTile / 4;
                    _rects.Add((Pixels(px + offset, py + offset, PixelsPerTile - offset * 2, PixelsPerTile - offset * 2), dot));
                }
            }
        }

        if (!any)
        {
            if (_chunks.Remove(chunk, out var empty))
                empty.Dispose();

            return;
        }

        if (!_chunks.TryGetValue(chunk, out var target))
        {
            const int size = ChunkSize * PixelsPerTile;
            _chunks[chunk] = target = _clyde.CreateRenderTarget(
                new Vector2i(size, size),
                new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb),
                new TextureSampleParameters { Filter = true },
                $"world-map-{chunk.X}-{chunk.Y}");
        }

        handle.RenderInRenderTarget(target,
            () =>
            {
                foreach (var (rect, color) in _rects)
                {
                    handle.DrawRect(rect, color);
                }
            },
            Color.Transparent);
    }

    private static UIBox2 Pixels(int x, int y, int width, int height)
        => UIBox2.FromDimensions(x, y, width, height);

    private Color? GetTileColor(Entity<MapGridComponent> map, Entity<MapGridComponent>? fowGrid, Vector2i tile)
    {
        if (_map.TryGetTileRef(map, map.Comp, tile, out var tileRef) && !tileRef.Tile.IsEmpty)
            return _turf.GetContentTileDefinition(tileRef).NavMapColor;

        if (fowGrid is { } fow && _map.TryGetTileRef(fow, fow.Comp, tile, out tileRef) && !tileRef.Tile.IsEmpty)
            return _turf.GetContentTileDefinition(tileRef).NavMapColor;

        return null;
    }

    private bool IsRoof(RoofComponent? roof, Vector2i tile)
    {
        if (_roofTiles.ContainsKey(tile))
            return true;

        if (roof == null)
            return false;

        var index = SharedMapSystem.GetChunkIndices(tile, RoofComponent.ChunkSize);

        if (!roof.Data.TryGetValue(index, out var mask))
            return false;

        var relative = SharedMapSystem.GetChunkRelative(tile, RoofComponent.ChunkSize);
        var flag = (ulong) 1 << (relative.X + relative.Y * RoofComponent.ChunkSize);

        return (mask & flag) == flag;
    }

    #endregion
}
