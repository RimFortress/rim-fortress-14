using System.Buffers;
using System.Collections.Concurrent;
using System.Numerics;
using Content.Server.Parallax;
using Content.Shared._RF;
using Content.Shared._RF.NPC.Pathfinding;
using Content.Shared._RF.NPC.Pathfinding.Components;
using Content.Shared.Damage;
using Content.Shared.Maps;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Physics;
using Robust.Shared.Collections;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.Pathfinding.Systems;

// Building of the chunks of the navigation map.
public sealed partial class RfPathfindingSystem
{
    [Dependency] private BiomeSystem _biome = null!;
    [Dependency] private EntityLookupSystem _lookup = null!;
    [Dependency] private FixtureSystem _fixtures = null!;
    [Dependency] private SharedMapSystem _maps = null!;
    [Dependency] private ITileDefinitionManager _tileDef = null!;

    [Dependency] private EntityQuery<FixturesComponent> _fixturesQuery;
    [Dependency] private EntityQuery<PathTagsComponent> _tagsQuery;

    // What relevant collision groups we track for pathfinding.
    // Stuff like chairs have collision but aren't relevant for mobs.
    private const int PathfindingCollisionMask = (int) CollisionGroup.MobMask;
    private const int PathfindingCollisionLayer = (int) CollisionGroup.MobLayer;

    // We won't do points on edges so we'll offset them slightly.
    private const float StepOffset = DistanceStep / 2f;

    private const int PointsPerSide = RfPathfindingChunk.PointsPerSide;

    // Caches of what is calculated from the prototypes, which are the same for every place they are used.
    // The chunks are built in parallel, so they have to be thread-safe.
    private readonly ConcurrentDictionary<string, VirtualEntity> _virtualEntities = new();
    private readonly ConcurrentDictionary<int, byte[]> _tileTags = new();
    private readonly ConcurrentDictionary<string, DamageSpecifier?> _destroyDamage = new();

    /// <summary>
    /// What a tile is, from the world's point of view.
    /// </summary>
    /// <param name="Flags">The state of the region.</param>
    /// <param name="TileType">The type of the tile, -1 if there is no tile.</param>
    /// <param name="Entity">The entity that the biome would put on the tile if it was loaded, if any.</param>
    private readonly record struct TileInfo(RfPathfindingFlags Flags, int TileType, string? Entity);

    /// <summary>
    /// What an entity that the biome puts on a tile does to the points of the tile. The biome puts the entity in
    /// the same way everywhere, so it is calculated once for each prototype.
    /// </summary>
    /// <param name="Layers">Collision layers of the points of the tile, null if the entity does not collide.</param>
    /// <param name="Masks">Collision masks of the points of the tile.</param>
    /// <param name="Destroy">Damage needed to destroy the entity, null if it cannot be.</param>
    /// <param name="Tags">Indices of the path tags of the entity.</param>
    private readonly record struct VirtualEntity(int[]? Layers, int[]? Masks, DamageSpecifier? Destroy, byte[] Tags);

    /// <summary>
    /// A built chunk, which is not on the map yet.
    /// </summary>
    /// <param name="Polys">Polys of each tile.</param>
    /// <param name="Sources">Path tags located on the tiles.</param>
    private readonly record struct BuiltChunk(List<RfPathPoly>?[] Polys, PathTagSource[] Sources);

    private void InitializeBaking()
    {
        Subs.ProtoReload<EntityPrototype>(_proto,
            () =>
            {
                _virtualEntities.Clear();
                _destroyDamage.Clear();
            });

        Subs.ProtoReload<ContentTileDefinition>(_proto, () => _tileTags.Clear());
    }

    /// <summary>
    /// Should the entity be a part of the navigation map, because it collides with those who walk.
    /// </summary>
    private static bool IsBodyRelevant(FixturesComponent fixtures)
    {
        foreach (var fixture in fixtures.Fixtures.Values)
        {
            if (!fixture.Hard)
                continue;

            if ((fixture.CollisionMask & PathfindingCollisionLayer) != 0x0 ||
                (fixture.CollisionLayer & PathfindingCollisionMask) != 0x0)
            {
                return true;
            }
        }

        return false;
    }

    private static int PointIndex(int tileX, int tileY, int subX, int subY)
        => RfPathfindingChunk.PointIndex(tileX * SubStep + subX, tileY * SubStep + subY);

    /// <summary>
    /// Builds the chunk from the world. It does not touch anything that exists, so the chunks can be built in
    /// parallel.
    /// </summary>
    /// <remarks>
    /// Where the biome has not created the tiles and entities, the biome is asked what it would create: the world
    /// is the same whether or not a part of it is loaded.
    /// </remarks>
    private BuiltChunk BuildChunk(
        Entity<MapGridComponent> grid,
        BiomeComponent? biome,
        RfGridPathfindingComponent map,
        Vector2i origin)
    {
        var gridOrigin = origin * ChunkSize;
        var radius = (int) MathF.Ceiling(MaxTagRadius);
        var points = ArrayPool<RfPathfindingData>.Shared.Rent(PointsPerSide * PointsPerSide);
        var sources = new ValueList<PathTagSource>();
        var polys = new List<RfPathPoly>?[RfPathfindingChunk.TileCount];

        try
        {
            var buckets = GatherEntities(grid, map, gridOrigin, radius, ref sources);
            Span<bool> indestructible = stackalloc bool[SubStep * SubStep];

            for (var x = 0; x < ChunkSize; x++)
            {
                for (var y = 0; y < ChunkSize; y++)
                {
                    var tilePos = gridOrigin + new Vector2i(x, y);
                    var info = DescribeTile(grid, biome, tilePos);

                    if (info.TileType >= 0)
                    {
                        foreach (var tag in GetTileTags(info.TileType))
                        {
                            sources.Add(new PathTagSource(tag, tilePos));
                        }
                    }

                    // Most of the tiles have nothing on them, so all their points are the same.
                    var tileData = new RfPathfindingData(info.Flags, 0, 0, default);

                    for (var subX = 0; subX < SubStep; subX++)
                    {
                        Array.Fill(points, tileData, PointIndex(x, y, subX, 0), SubStep);
                    }

                    indestructible.Clear();

                    if (info.Entity != null)
                        ApplyVirtualEntity(points, x, y, GetVirtualEntity(info.Entity), tilePos, indestructible, ref sources);

                    if (buckets[x * ChunkSize + y] is { } bucket)
                    {
                        foreach (var (ent, fixtures) in bucket)
                        {
                            ApplyEntity(points, x, y, tilePos, ent, fixtures, indestructible);
                        }
                    }
                }
            }

            GatherNeighborSources(grid, biome, map, origin, radius, ref sources);
            StampInfluence(points, gridOrigin, sources.Span);
            BuildPolys(points, grid.Owner, origin, polys);

            return new BuiltChunk(polys, sources.Span.ToArray());
        }
        finally
        {
            // Not to hold the damage specifiers of the points.
            ArrayPool<RfPathfindingData>.Shared.Return(points, true);
        }
    }

    /// <summary>
    /// Finds the entities that matter for the chunk with one query for the whole chunk: those that collide
    /// are put into the buckets of their tiles, and the path tags of those around are added to the sources.
    /// </summary>
    private List<(EntityUid, FixturesComponent)>?[] GatherEntities(
        Entity<MapGridComponent> grid,
        RfGridPathfindingComponent map,
        Vector2i gridOrigin,
        int radius,
        ref ValueList<PathTagSource> sources)
    {
        var buckets = new List<(EntityUid, FixturesComponent)>?[RfPathfindingChunk.TileCount];
        var entities = new HashSet<EntityUid>();
        var box = new Box2(
            gridOrigin.X - radius,
            gridOrigin.Y - radius,
            gridOrigin.X + ChunkSize + radius,
            gridOrigin.Y + ChunkSize + radius);

        _lookup.GetLocalEntitiesIntersecting(grid.Owner, box, entities);

        foreach (var ent in entities)
        {
            // The bodies that are being deleted are still found, they are the reason for the rebuild.
            if (TerminatingOrDeleted(ent))
                continue;

            var xform = Transform(ent);

            if (xform.ParentUid != grid.Owner)
                continue;

            var tile = _maps.LocalToTile(grid.Owner, grid.Comp, xform.Coordinates);
            var local = tile - gridOrigin;
            var inside = local.X >= 0 && local.Y >= 0 && local.X < ChunkSize && local.Y < ChunkSize;

            // The sources of an existing neighbor chunk are taken from it.
            if (_tagsQuery.TryComp(ent, out var tags)
                && (inside || !map.Chunks.ContainsKey(SharedMapSystem.GetChunkIndices(tile, ChunkSize))))
            {
                foreach (var tag in tags.Tags)
                {
                    if (TryGetTagIndex(tag, out var index))
                        sources.Add(new PathTagSource((byte) index, tile));
                }
            }

            if (!inside || !_fixturesQuery.TryComp(ent, out var fixtures) || !IsBodyRelevant(fixtures))
                continue;

            (buckets[local.X * ChunkSize + local.Y] ??= new()).Add((ent, fixtures));
        }

        return buckets;
    }

    /// <summary>
    /// Adds the path tags that are located close enough to the chunk, to influence it, but are not a part of it.
    /// </summary>
    private void GatherNeighborSources(
        Entity<MapGridComponent> grid,
        BiomeComponent? biome,
        RfGridPathfindingComponent map,
        Vector2i origin,
        int radius,
        ref ValueList<PathTagSource> sources)
    {
        if (radius <= 0)
            return;

        var min = origin * ChunkSize - new Vector2i(radius, radius);
        var max = (origin + Vector2i.One) * ChunkSize + new Vector2i(radius, radius);

        for (var chunkX = origin.X - 1; chunkX <= origin.X + 1; chunkX++)
        {
            for (var chunkY = origin.Y - 1; chunkY <= origin.Y + 1; chunkY++)
            {
                var neighbor = new Vector2i(chunkX, chunkY);

                if (neighbor == origin)
                    continue;

                if (map.Chunks.TryGetValue(neighbor, out var chunk))
                {
                    foreach (var source in chunk.Sources)
                    {
                        if (source.Tile.X >= min.X && source.Tile.X < max.X && source.Tile.Y >= min.Y && source.Tile.Y < max.Y)
                            sources.Add(source);
                    }

                    continue;
                }

                // The chunk is not built, so the tiles are asked what they are. Entities with tags are found
                // by the query of the chunk.
                for (var x = Math.Max(min.X, neighbor.X * ChunkSize); x < Math.Min(max.X, (neighbor.X + 1) * ChunkSize); x++)
                {
                    for (var y = Math.Max(min.Y, neighbor.Y * ChunkSize); y < Math.Min(max.Y, (neighbor.Y + 1) * ChunkSize); y++)
                    {
                        var tile = new Vector2i(x, y);
                        var info = DescribeTile(grid, biome, tile);

                        if (info.TileType >= 0)
                        {
                            foreach (var tag in GetTileTags(info.TileType))
                            {
                                sources.Add(new PathTagSource(tag, tile));
                            }
                        }

                        if (info.Entity == null)
                            continue;

                        foreach (var tag in GetVirtualEntity(info.Entity).Tags)
                        {
                            sources.Add(new PathTagSource(tag, tile));
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Determines what is on the tile. Where the grid has no tile, the biome is asked what it would put there.
    /// </summary>
    private TileInfo DescribeTile(Entity<MapGridComponent> grid, BiomeComponent? biome, Vector2i tilePos)
    {
        var tile = _maps.GetTileRef(grid.Owner, grid.Comp, tilePos).Tile;

        if (!tile.IsEmpty)
            return new TileInfo(RfPathfindingFlags.None, tile.TypeId, null);

        // The tile was emptied on purpose or the biome has nothing here.
        if (biome == null
            || _biome.IsTileModified(biome, tilePos)
            || !_biome.TryGetBiomeTile(tilePos, biome.Layers, biome.Seed, grid, out var biomeTile)
            || biomeTile.Value.IsEmpty)
            return new TileInfo(RfPathfindingFlags.Space, -1, null);

        _biome.TryGetEntity(tilePos, biome, grid, out var entity);
        return new TileInfo(RfPathfindingFlags.None, biomeTile.Value.TypeId, entity);
    }

    private byte[] GetTileTags(int tileType) => _tileTags.GetOrAdd(tileType,
        type =>
        {
            if (!_tileDef.TryGetDefinition(type, out var definition))
                return Array.Empty<byte>();

            var tags = new List<byte>();

            foreach (var tag in ((ContentTileDefinition)definition).PathTags)
            {
                if (TryGetTagIndex(tag, out var index))
                    tags.Add((byte)index);
            }

            return tags.ToArray();
        });


    private VirtualEntity GetVirtualEntity(string proto) => _virtualEntities.GetOrAdd(proto, CreateVirtualEntity);

    private VirtualEntity CreateVirtualEntity(string protoId)
    {
        if (!_proto.TryIndex<EntityPrototype>(protoId, out var proto))
            return new VirtualEntity(null, null, null, Array.Empty<byte>());

        var tags = new List<byte>();

        if (proto.TryComp<PathTagsComponent>(out var tagsComp, EntityManager.ComponentFactory))
        {
            foreach (var tag in tagsComp.Tags)
            {
                if (TryGetTagIndex(tag, out var index))
                    tags.Add((byte) index);
            }
        }

        int[]? layers = null;
        int[]? masks = null;

        // The biome puts the entity in the center of the tile, without rotation.
        if (!proto.TryComp<FixturesComponent>(out var fixtures, EntityManager.ComponentFactory)
            || !IsBodyRelevant(fixtures))
            return new VirtualEntity(layers, masks, GetDestroyDamage(proto), tags.ToArray());

        var transform = new Transform(new Vector2(0.5f, 0.5f), Angle.Zero);

        for (var subX = 0; subX < SubStep; subX++)
        {
            for (var subY = 0; subY < SubStep; subY++)
            {
                var point = new Vector2(StepOffset + (float) subX / SubStep, StepOffset + (float) subY / SubStep);

                foreach (var fixture in fixtures.Fixtures.Values)
                {
                    if (!fixture.Hard || !_fixtures.TestPoint(fixture.Shape, transform, point))
                        continue;

                    layers ??= new int[SubStep * SubStep];
                    masks ??= new int[SubStep * SubStep];
                    layers[subX * SubStep + subY] |= fixture.CollisionLayer;
                    masks[subX * SubStep + subY] |= fixture.CollisionMask;
                }
            }
        }

        return new VirtualEntity(layers, masks, GetDestroyDamage(proto), tags.ToArray());
    }

    private static void ApplyVirtualEntity(
        RfPathfindingData[] points,
        int tileX,
        int tileY,
        in VirtualEntity entity,
        Vector2i tilePos,
        Span<bool> indestructible,
        ref ValueList<PathTagSource> sources)
    {
        foreach (var tag in entity.Tags)
        {
            sources.Add(new PathTagSource(tag, tilePos));
        }

        if (entity.Layers == null || entity.Masks == null)
            return;

        for (var i = 0; i < SubStep * SubStep; i++)
        {
            if (entity.Layers[i] == 0 && entity.Masks[i] == 0)
                continue;

            ref var data = ref points[PointIndex(tileX, tileY, i / SubStep, i % SubStep)];
            data.CollisionLayer |= entity.Layers[i];
            data.CollisionMask |= entity.Masks[i];
            AddDestroyDamage(ref data, ref indestructible[i], entity.Destroy);
        }
    }

    private void ApplyEntity(
        RfPathfindingData[] points,
        int tileX,
        int tileY,
        Vector2i tilePos,
        EntityUid ent,
        FixturesComponent fixtures,
        Span<bool> indestructible)
    {
        var xform = Transform(ent);
        var transform = new Transform(xform.LocalPosition, xform.LocalRotation);
        var destroy = GetEntityDestroyDamage(ent);

        for (var subX = 0; subX < SubStep; subX++)
        {
            for (var subY = 0; subY < SubStep; subY++)
            {
                var point = new Vector2(
                    StepOffset + tilePos.X + (float) subX / SubStep,
                    StepOffset + tilePos.Y + (float) subY / SubStep);
                var layer = 0;
                var mask = 0;
                var colliding = false;

                foreach (var fixture in fixtures.Fixtures.Values)
                {
                    if (!fixture.Hard)
                        continue;

                    // Do an AABB check first as it's probably faster, then do an actual point check.
                    var intersects = false;

                    foreach (var proxy in fixture.Proxies)
                    {
                        if (!proxy.AABB.Contains(point))
                            continue;

                        intersects = true;
                        break;
                    }

                    // If the entity doesn't intersect this point (e.g. thindows) then ignore it.
                    if (!intersects || !_fixtures.TestPoint(fixture.Shape, transform, point))
                        continue;

                    layer |= fixture.CollisionLayer;
                    mask |= fixture.CollisionMask;
                    colliding = true;
                }

                if (!colliding)
                    continue;

                ref var data = ref points[PointIndex(tileX, tileY, subX, subY)];
                data.CollisionLayer |= layer;
                data.CollisionMask |= mask;
                AddDestroyDamage(ref data, ref indestructible[subX * SubStep + subY], destroy);
            }
        }
    }

    /// <summary>
    /// Adds an entity that collides at a point to what has to be destroyed to pass through the point: if there are
    /// several, all of them have to be, and if any of them cannot be destroyed, the point cannot.
    /// </summary>
    private static void AddDestroyDamage(ref RfPathfindingData data, ref bool indestructible, DamageSpecifier? destroy)
    {
        if (indestructible)
            return;

        if (destroy == null)
        {
            indestructible = true;
            data.Facts.Destroy = null;
            return;
        }

        data.Facts.Destroy = data.Facts.Destroy == null ? destroy : data.Facts.Destroy + destroy;
    }

    private DamageSpecifier? GetEntityDestroyDamage(EntityUid ent)
        => MetaData(ent).EntityPrototype is { } proto ? GetDestroyDamage(proto) : GetDestroyDamage(ent);

    /// <summary>
    /// Writes the influence of the path tags to the points they reach.
    /// </summary>
    private void StampInfluence(RfPathfindingData[] points, Vector2i gridOrigin, ReadOnlySpan<PathTagSource> sources)
    {
        foreach (var source in sources)
        {
            var radius = GetTagRadius(source.Tag);
            var tile = source.Tile;
            var minX = Math.Max(0, (int) MathF.Floor((tile.X - radius - gridOrigin.X) * SubStep));
            var maxX = Math.Min(PointsPerSide - 1, (int) MathF.Ceiling((tile.X + 1 + radius - gridOrigin.X) * SubStep));
            var minY = Math.Max(0, (int) MathF.Floor((tile.Y - radius - gridOrigin.Y) * SubStep));
            var maxY = Math.Min(PointsPerSide - 1, (int) MathF.Ceiling((tile.Y + 1 + radius - gridOrigin.Y) * SubStep));

            for (var x = minX; x <= maxX; x++)
            {
                var pointX = gridOrigin.X + StepOffset + (float) x / SubStep;
                var dx = MathF.Max(MathF.Max(tile.X - pointX, 0f), pointX - tile.X - 1f);

                for (var y = minY; y <= maxY; y++)
                {
                    var pointY = gridOrigin.Y + StepOffset + (float) y / SubStep;
                    var dy = MathF.Max(MathF.Max(tile.Y - pointY, 0f), pointY - tile.Y - 1f);
                    var distance = MathF.Sqrt(dx * dx + dy * dy);

                    if (distance <= radius)
                        AddInfluence(ref points[RfPathfindingChunk.PointIndex(x, y)].Facts, source.Tag, distance);
                }
            }
        }
    }

    /// <summary>
    /// Merges the points of each tile into polys: the points that are the same are united into rectangles.
    /// </summary>
    private static void BuildPolys(RfPathfindingData[] points, EntityUid graph, Vector2i origin, List<RfPathPoly>?[] polys)
    {
        var gridOrigin = origin * ChunkSize;
        var tilePolys = new ValueList<Box2i>(SubStep);

        for (var x = 0; x < ChunkSize; x++)
        {
            for (var y = 0; y < ChunkSize; y++)
            {
                var polyOffset = gridOrigin + new Vector2(x, y);
                var tileIndex = (byte) (x * ChunkSize + y);
                var first = points[PointIndex(x, y, 0, 0)];
                var uniform = true;

                for (var i = 1; i < SubStep * SubStep && uniform; i++)
                {
                    uniform = first.Equals(points[PointIndex(x, y, i / SubStep, i % SubStep)]);
                }

                // The most common case: the whole tile is one poly.
                if (uniform)
                {
                    polys[tileIndex] = new List<RfPathPoly>(1)
                    {
                        new(graph, origin, tileIndex, new Box2(polyOffset, polyOffset + Vector2.One), first),
                    };

                    continue;
                }

                tilePolys.Clear();
                var data = first;
                var start = Vector2i.Zero;

                // Lines of equal points along the Y axis.
                for (var i = 0; i < SubStep * SubStep; i++)
                {
                    var ix = i / SubStep;
                    var iy = i % SubStep;
                    var nextX = (i + 1) / SubStep;
                    var nextY = (i + 1) % SubStep;

                    // End point
                    if (iy != SubStep - 1 && points[PointIndex(x, y, nextX, nextY)].Equals(data))
                        continue;

                    tilePolys.Add(new Box2i(start, new Vector2i(ix, iy)));

                    if (i >= SubStep * SubStep - 1)
                        continue;

                    start = new Vector2i(nextX, nextY);
                    data = points[PointIndex(x, y, nextX, nextY)];
                }

                // Now combine the lines
                var anyCombined = true;

                while (anyCombined)
                {
                    anyCombined = false;

                    for (var i = 0; i < tilePolys.Count; i++)
                    {
                        var poly = tilePolys[i];
                        data = points[PointIndex(x, y, poly.Left, poly.Bottom)];

                        for (var j = i + 1; j < tilePolys.Count; j++)
                        {
                            var nextPoly = tilePolys[j];
                            var nextData = points[PointIndex(x, y, nextPoly.Left, nextPoly.Bottom)];

                            if (poly.Bottom != nextPoly.Bottom ||
                                poly.Top != nextPoly.Top ||
                                poly.Right + 1 != nextPoly.Left ||
                                !data.Equals(nextData))
                                continue;

                            tilePolys.RemoveAt(j);
                            j--;
                            poly = new Box2i(poly.Left, poly.Bottom, poly.Right + 1, poly.Top);
                            anyCombined = true;
                        }

                        tilePolys[i] = poly;
                    }
                }

                var tile = polys[tileIndex] = new List<RfPathPoly>(tilePolys.Count);

                foreach (var poly in tilePolys)
                {
                    var box = new Box2((Vector2) poly.BottomLeft / SubStep + polyOffset,
                        (Vector2) (poly.TopRight + Vector2i.One) / SubStep + polyOffset);
                    var polyData = points[PointIndex(x, y, poly.Left, poly.Bottom)];

                    tile.Add(new RfPathPoly(graph, origin, tileIndex, box, polyData));
                }
            }
        }
    }
}
