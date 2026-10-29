using System.Numerics;
using System.Threading.Tasks;
using Content.Shared._RF;
using Content.Shared._RF.NPC.Pathfinding;
using Content.Shared._RF.NPC.Pathfinding.Components;
using Content.Shared.Parallax.Biomes;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;
using Robust.Shared.Threading;

namespace Content.Server._RF.NPC.Pathfinding.Systems;

// The navigation map of the grids: when its chunks appear, change and disappear.
public sealed partial class RfPathfindingSystem
{
    [Dependency] private IGameTiming _timing = null!;
    [Dependency] private IParallelManager _parallel = null!;
    [Dependency] private SharedTransformSystem _transform = null!;

    [Dependency] private EntityQuery<RfGridPathfindingComponent> _pathGridQuery;
    [Dependency] private EntityQuery<MapGridComponent> _mapGridQuery;
    [Dependency] private EntityQuery<BiomeComponent> _biomeQuery;

    private static readonly TimeSpan EvictionInterval = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The changes the biome makes by loading and unloading its chunks do not change the world for the
    /// pathfinding, since the map is built from what the biome would create.
    /// </summary>
    protected override bool SuppressDirtying => _biome.IsProcessingChunk;

    #region Events

    [SubscribeLocalEvent]
    private void OnGridInit(GridInitializeEvent ev)
    {
        EnsureComp<RfGridPathfindingComponent>(ev.EntityUid);
    }

    [SubscribeLocalEvent]
    private void OnGridRemoved(GridRemovalEvent ev)
    {
        RemComp<RfGridPathfindingComponent>(ev.EntityUid);
    }

    [SubscribeLocalEvent]
    private void OnGridPathShutdown(Entity<RfGridPathfindingComponent> ent, ref ComponentShutdown args)
    {
        // Invalidate all polygons, anything that holds them has to know they are gone.
        ClearMap(ent.Comp);
    }

    [SubscribeLocalEvent]
    private void OnTileChanged(ref TileChangedEvent ev)
    {
        foreach (var (newTile, oldTile, _, tile) in ev.Changes)
        {
            if (oldTile.TypeId == newTile.TypeId)
                continue;

            // The tile's path tags influence the area around it.
            MarkDirty(ev.Entity,
                new Box2(
                    tile.X - MaxTagRadius,
                    tile.Y - MaxTagRadius,
                    tile.X + 1 + MaxTagRadius,
                    tile.Y + 1 + MaxTagRadius));
        }
    }

    [SubscribeLocalEvent]
    private void OnCollisionChange(ref CollisionChangeEvent ev)
    {
        MarkBodyDirty(ev.BodyUid);
    }

    [SubscribeLocalEvent]
    private void OnCollisionLayerChange(ref CollisionLayerChangeEvent ev)
    {
        MarkBodyDirty(ev.Body);
    }

    [SubscribeLocalEvent]
    private void OnBodyTypeChange(ref PhysicsBodyTypeChangedEvent ev)
    {
        MarkBodyDirty(ev.Entity);
    }

    private void MarkBodyDirty(EntityUid body)
    {
        var xform = Transform(body);

        if (xform.GridUid is not { } grid)
            return;

        // This will also rebuild on door open / closes.
        MarkDirty(grid, _lookup.GetAABBNoContainer(body, xform.Coordinates.Position, xform.LocalRotation));
    }

    private void OnMoveEvent(ref MoveEvent ev)
    {
        if (!_fixturesQuery.TryComp(ev.Sender, out var fixtures) ||
            !IsBodyRelevant(fixtures) ||
            _mapGridQuery.HasComp(ev.Sender))
            return;

        var gridUid = ev.Component.GridUid;
        var oldGridUid = ev.OldPosition.EntityId == ev.NewPosition.EntityId
            ? gridUid
            : _transform.GetGrid((ev.Entity.Owner, ev.Component));

        if (oldGridUid != null && oldGridUid != gridUid)
            MarkDirty(oldGridUid.Value, _lookup.GetAABBNoContainer(ev.Sender, ev.OldPosition.Position, ev.OldRotation));

        if (gridUid != null)
            MarkDirty(gridUid.Value, _lookup.GetAABBNoContainer(ev.Sender, ev.NewPosition.Position, ev.NewRotation));
    }

    #endregion

    #region Chunks

    /// <summary>
    /// Returns the chunk of the grid's navigation map, building it from the world if it is not on the map.
    /// </summary>
    /// <param name="gridUid">The grid.</param>
    /// <param name="origin">The origin of the chunk.</param>
    /// <returns>The chunk, null if the grid has no navigation map.</returns>
    [PublicAPI]
    public RfPathfindingChunk? EnsureChunk(EntityUid gridUid, Vector2i origin)
    {
        if (!_pathGridQuery.TryComp(gridUid, out var map) || !_mapGridQuery.TryComp(gridUid, out var grid))
            return null;

        var now = _timing.CurTime;

        if (map.Chunks.TryGetValue(origin, out var chunk))
        {
            chunk.LastUsed = now;
            return chunk;
        }

        _biomeQuery.TryComp(gridUid, out var biome);
        var built = BuildChunk(new Entity<MapGridComponent>(gridUid, grid), biome, map, origin);

        chunk = new RfPathfindingChunk(origin) { LastUsed = now };
        map.Chunks[origin] = chunk;
        LinkTiles(map, chunk, Commit(chunk, built));

        return chunk;
    }

    /// <summary>
    /// Builds the chunks next to the poly if it is on the border of its chunk and they are not on the map yet,
    /// so that all the polys it leads to are known. Chunks that are on the map are marked as used.
    /// </summary>
    /// <param name="poly">The poly whose neighbors are needed.</param>
    /// <returns>How many chunks were built, so that a search can count them against its limit.</returns>
    [PublicAPI]
    public int EnsureNeighbors(RfPathPoly poly)
    {
        var x = poly.TileIndex / ChunkSize;
        var y = poly.TileIndex % ChunkSize;
        var built = 0;

        if (x == 0)
            built += EnsureNeighbor(poly, new Vector2i(-1, 0));

        if (x == ChunkSize - 1)
            built += EnsureNeighbor(poly, new Vector2i(1, 0));

        if (y == 0)
            built += EnsureNeighbor(poly, new Vector2i(0, -1));

        if (y == ChunkSize - 1)
            built += EnsureNeighbor(poly, new Vector2i(0, 1));

        return built;
    }

    private int EnsureNeighbor(RfPathPoly poly, Vector2i direction)
    {
        var origin = poly.ChunkOrigin + direction;

        if (!_pathGridQuery.TryComp(poly.GraphUid, out var map) || !map.Chunks.ContainsKey(origin))
            return EnsureChunk(poly.GraphUid, origin) == null ? 0 : 1;

        EnsureChunk(poly.GraphUid, origin);
        return 0;
    }

    /// <summary>
    /// Returns the poly that contains the point.
    /// </summary>
    /// <param name="coordinates">The point.</param>
    /// <param name="build">Build the chunk of the point if it is not on the map yet.</param>
    /// <returns>The poly, null if there is none.</returns>
    [PublicAPI]
    public RfPathPoly? GetPoly(EntityCoordinates coordinates, bool build = false)
    {
        if (_transform.GetGrid(coordinates) is not { } gridUid || !_pathGridQuery.TryComp(gridUid, out var map))
            return null;

        var local = Vector2.Transform(
            _transform.ToMapCoordinates(coordinates).Position,
            _transform.GetInvWorldMatrix(gridUid));
        var origin = new Vector2i((int) MathF.Floor(local.X / ChunkSize), (int) MathF.Floor(local.Y / ChunkSize));

        if (map.Chunks.TryGetValue(origin, out var chunk))
            chunk.LastUsed = _timing.CurTime;
        else if (!build || (chunk = EnsureChunk(gridUid, origin)) == null)
            return null;

        var tile = (int) MathHelper.Mod(local.X, ChunkSize) * ChunkSize + (int) MathHelper.Mod(local.Y, ChunkSize);

        foreach (var poly in chunk.GetPolys(tile))
        {
            if (poly.Box.Contains(local))
                return poly;
        }

        return null;
    }

    /// <summary>
    /// Marks the poly's chunk as used, so it is not removed while a search goes through it.
    /// </summary>
    /// <param name="poly">The poly.</param>
    [PublicAPI]
    public void Touch(RfPathPoly poly)
    {
        if (_pathGridQuery.TryComp(poly.GraphUid, out var map) && map.Chunks.TryGetValue(poly.ChunkOrigin, out var chunk))
            chunk.LastUsed = _timing.CurTime;
    }

    /// <summary>
    /// Rebuilds the chunks that are on the map and were marked as the ones to rebuild.
    /// </summary>
    private void RebuildDirty(Entity<RfGridPathfindingComponent, MapGridComponent> ent, ParallelOptions options)
    {
        var map = ent.Comp1;
        var chunks = new List<RfPathfindingChunk>(map.DirtyChunks.Count);

        foreach (var origin in map.DirtyChunks)
        {
            if (map.Chunks.TryGetValue(origin, out var chunk))
                chunks.Add(chunk);
        }

        map.DirtyChunks.Clear();

        if (chunks.Count == 0)
            return;

        _biomeQuery.TryComp(ent, out var biome);
        var grid = new Entity<MapGridComponent>(ent.Owner, ent.Comp2);
        var built = new BuiltChunk[chunks.Count];

        // Building is the costly part, and the chunks do not depend on each other.
        if (chunks.Count == 1)
            built[0] = BuildChunk(grid, biome, map, chunks[0].Origin);
        else
            Parallel.For(0, chunks.Count, options, i => built[i] = BuildChunk(grid, biome, map, chunks[i].Origin));

        var changed = new ulong[chunks.Count];

        for (var i = 0; i < chunks.Count; i++)
        {
            changed[i] = Commit(chunks[i], built[i]);
        }

        // The links are made after all the polys have been replaced.
        for (var i = 0; i < chunks.Count; i++)
        {
            LinkTiles(map, chunks[i], changed[i]);
        }
    }

    /// <summary>
    /// Removes the chunks that have not been used for too long. The map is a cache of the world and a removed
    /// chunk is built again when it is needed.
    /// </summary>
    private static void Evict(RfGridPathfindingComponent map, TimeSpan now)
    {
        map.NextEviction = now + EvictionInterval;
        List<Vector2i>? expired = null;

        foreach (var (origin, chunk) in map.Chunks)
        {
            if (now - chunk.LastUsed > map.ChunkLifetime)
                (expired ??= new List<Vector2i>()).Add(origin);
        }

        if (expired == null)
            return;

        foreach (var origin in expired)
        {
            ClearChunk(map.Chunks[origin]);
            map.Chunks.Remove(origin);
            map.DirtyChunks.Remove(origin);
        }
    }

    private void ClearMaps()
    {
        var query = AllEntityQuery<RfGridPathfindingComponent>();

        while (query.MoveNext(out _, out var map))
        {
            ClearMap(map);
        }
    }

    private static void ClearMap(RfGridPathfindingComponent map)
    {
        foreach (var chunk in map.Chunks.Values)
        {
            ClearChunk(chunk);
        }

        map.Chunks.Clear();
        map.DirtyChunks.Clear();
    }

    private static void ClearChunk(RfPathfindingChunk chunk)
    {
        foreach (var polys in chunk.Polys)
        {
            if (polys != null)
                ClearTilePolys(polys);
        }
    }

    #endregion

    #region Polys

    /// <summary>
    /// Puts the built polys into the chunk. A tile whose polys are the same regions as before keeps them,
    /// and with them their links, only taking the new facts.
    /// </summary>
    /// <returns>The mask of the tiles whose polys have been replaced.</returns>
    private static ulong Commit(RfPathfindingChunk chunk, in BuiltChunk built)
    {
        ulong changed = 0;

        for (var i = 0; i < RfPathfindingChunk.TileCount; i++)
        {
            var fresh = built.Polys[i];
            var existing = chunk.Polys[i];

            if (existing != null && fresh != null && existing.Count == fresh.Count)
            {
                var same = true;

                for (var j = 0; j < existing.Count && same; j++)
                {
                    same = existing[j].IsEquivalent(fresh[j]);
                }

                if (same)
                {
                    for (var j = 0; j < existing.Count; j++)
                    {
                        existing[j].Data = fresh[j].Data;
                    }

                    continue;
                }
            }

            if (existing != null)
                ClearTilePolys(existing);

            chunk.Polys[i] = fresh;
            changed |= 1UL << i;
        }

        chunk.Sources = built.Sources;
        return changed;
    }

    /// <summary>
    /// Clears all of the polygons on a tile.
    /// </summary>
    private static void ClearTilePolys(List<RfPathPoly> polys)
    {
        foreach (var poly in polys)
        {
            ClearPoly(poly);
        }

        polys.Clear();
    }

    /// <summary>
    /// Clears a polygon and invalidates it, if anyone still has a reference to it.
    /// </summary>
    private static void ClearPoly(RfPathPoly poly)
    {
        foreach (var neighbor in poly.Neighbors)
        {
            neighbor.Neighbors.Remove(poly);
        }

        // If any paths have a ref to it let them know that the class is no longer a valid node.
        poly.Data.Flags = RfPathfindingFlags.Invalid;
        poly.Neighbors.Clear();
    }

    /// <summary>
    /// Links the polys of the tiles in the mask to the polys that touch them.
    /// </summary>
    private static void LinkTiles(RfGridPathfindingComponent map, RfPathfindingChunk chunk, ulong tiles)
    {
        while (tiles != 0)
        {
            var tile = BitOperations.TrailingZeroCount(tiles);
            tiles &= tiles - 1;
            LinkTile(map, chunk, tile / ChunkSize, tile % ChunkSize);
        }
    }

    private static void LinkTile(RfGridPathfindingComponent map, RfPathfindingChunk chunk, int x, int y)
    {
        var tile = chunk.Polys[x * ChunkSize + y];

        if (tile == null)
            return;

        for (var i = 0; i < tile.Count; i++)
        {
            var poly = tile[i];

            for (var j = i + 1; j < tile.Count; j++)
            {
                LinkPolys(poly, tile[j]);
            }

            LinkTileNeighbor(map, chunk, poly, x - 1, y);
            LinkTileNeighbor(map, chunk, poly, x + 1, y);
            LinkTileNeighbor(map, chunk, poly, x, y - 1);
            LinkTileNeighbor(map, chunk, poly, x, y + 1);
        }
    }

    private static void LinkTileNeighbor(RfGridPathfindingComponent map, RfPathfindingChunk chunk, RfPathPoly poly, int x, int y)
    {
        var origin = chunk.Origin;

        // The tile is in a neighboring chunk.
        if (x < 0 || y < 0 || x >= ChunkSize || y >= ChunkSize)
        {
            origin += new Vector2i(x < 0 ? -1 : x >= ChunkSize ? 1 : 0, y < 0 ? -1 : y >= ChunkSize ? 1 : 0);
            x = (x + ChunkSize) % ChunkSize;
            y = (y + ChunkSize) % ChunkSize;
        }

        var target = origin == chunk.Origin
            ? chunk
            : map.Chunks.GetValueOrDefault(origin);

        if (target?.Polys[x * ChunkSize + y] is not { } neighbors)
            return;

        foreach (var neighbor in neighbors)
        {
            LinkPolys(poly, neighbor);
        }
    }

    private static void LinkPolys(RfPathPoly polyA, RfPathPoly polyB)
    {
        // Need to ensure they intersect by at least 2 tiles.
        var overlap = Box2.Area(polyA.Box.Enlarged(StepOffset).Intersect(polyB.Box.Enlarged(StepOffset)));

        if (overlap <= 0.5f / SubStep)
            return;

        polyA.AddNeighbor(polyB);
        polyB.AddNeighbor(polyA);
    }

    #endregion
}
