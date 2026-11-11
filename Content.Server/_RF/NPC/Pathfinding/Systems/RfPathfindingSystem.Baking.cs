using System.Buffers;
using System.Collections.Concurrent;
using System.Linq;
using System.Numerics;
using Content.Server.Parallax;
using Content.Shared._RF;
using Content.Shared._RF.NPC.Pathfinding;
using Content.Shared._RF.NPC.Pathfinding.Components;
using Content.Shared._RF.NPC.Pathfinding.Prototypes;
using Content.Shared.Damage;
using Content.Shared.Maps;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Physics;
using Robust.Shared.Collections;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
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
    [Dependency] private EntityQuery<PhysicsComponent> _physicsQuery;
    [Dependency] private EntityQuery<PathTagsComponent> _tagsQuery;

    /// <summary>
    /// The tag that every tile with a body that does not move is the source of, its influence is how close to such
    /// a body a node is. A profile can make the nodes near such bodies cost more, so that the path keeps away from
    /// them as far as the size of the agent needs.
    /// </summary>
    public static readonly ProtoId<PathTagPrototype> ObstacleTag = "Obstacle";

    // What relevant collision groups we track for pathfinding.
    // Stuff like chairs have collision but aren't relevant for mobs.
    private const int PathfindingCollisionMask = (int) CollisionGroup.MobMask;
    private const int PathfindingCollisionLayer = (int) CollisionGroup.MobLayer;

    // We won't do points on edges so we'll offset them slightly.
    private const float StepOffset = DistanceStep / 2f;

    private const int PointsPerSide = RfPathfindingChunk.PointsPerSide;

    // How much the region of a body is made smaller to find the tiles it is in: the skin of a polygon is up to 0.01.
    private const float BoundsSkin = 0.03f;

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
    /// <param name="Layers">
    /// Collision layers of the points of the tile of the entity and of the tiles around it, by <see cref="FootprintIndex"/>,
    /// null where the entity does not reach.
    /// </param>
    /// <param name="Masks">Collision masks of the points, as the layers.</param>
    /// <param name="Destroy">Damage needed to destroy the entity, null if it cannot be.</param>
    /// <param name="Tags">Indices of the path tags of the entity.</param>
    private readonly record struct VirtualEntity(int[]?[] Layers, int[]?[] Masks, DamageSpecifier? Destroy, byte[] Tags, bool Soft);

    // The tiles around the tile of a virtual entity, that it can reach into: the body can be bigger than a tile.
    private const int FootprintSide = 3;
    private const int FootprintSize = FootprintSide * FootprintSide;

    private static int FootprintIndex(int offsetX, int offsetY) => (offsetX + 1) * FootprintSide + offsetY + 1;

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
                return true;
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
            var obstacle = TryGetTagIndex(ObstacleTag, out var obstacleIndex) ? obstacleIndex : -1;
            Span<bool> indestructible = stackalloc bool[SubStep * SubStep];

            // What the biome would put on the tiles of the chunk and on the ones around it: the bodies are bigger than the
            // tiles they are on, and reach into the next ones.
            var infos = new TileInfo[(ChunkSize + 2) * (ChunkSize + 2)];

            for (var x = -1; x <= ChunkSize; x++)
            {
                for (var y = -1; y <= ChunkSize; y++)
                {
                    infos[(x + 1) * (ChunkSize + 2) + y + 1] = DescribeTile(grid, biome, gridOrigin + new Vector2i(x, y));
                }
            }

            for (var x = 0; x < ChunkSize; x++)
            {
                for (var y = 0; y < ChunkSize; y++)
                {
                    var tilePos = gridOrigin + new Vector2i(x, y);
                    var info = infos[(x + 1) * (ChunkSize + 2) + y + 1];

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
                    var solid = false;

                    if (info.Entity != null)
                    {
                        foreach (var tag in GetVirtualEntity(info.Entity).Tags)
                        {
                            sources.Add(new PathTagSource(tag, tilePos));
                        }
                    }

                    for (var neighborX = -1; neighborX <= 1; neighborX++)
                    {
                        for (var neighborY = -1; neighborY <= 1; neighborY++)
                        {
                            if (infos[(x + 1 + neighborX) * (ChunkSize + 2) + y + 1 + neighborY].Entity is not { } neighbor)
                                continue;

                            // The tile is at the opposite offset from the tile of the entity.
                            ApplyFootprint(points, x, y, GetVirtualEntity(neighbor), -neighborX, -neighborY, indestructible, ref solid);
                        }
                    }

                    if (buckets[x * ChunkSize + y] is { } bucket)
                    {
                        foreach (var (ent, fixtures) in bucket)
                        {
                            ApplyEntity(points, x, y, tilePos, ent, fixtures, indestructible, ref solid);
                        }
                    }

                    // The path is made at a distance from what does not move, an agent has a size.
                    if (solid && obstacle >= 0)
                        sources.Add(new PathTagSource((byte) obstacle, tilePos));
                }
            }

            GatherNeighborSources(grid, biome, map, origin, radius, ref sources);
            StampInfluence(points, gridOrigin, sources.Span);
            BuildPolys(points, grid.Owner, origin, polys);

            // Only what is in the chunk is kept for the chunks next to it. The sources taken from them are theirs, they would
            // come back after the chunk they came from has changed.
            var own = new List<PathTagSource>(sources.Span.Length);

            foreach (var source in sources.Span)
            {
                var local = source.Tile - gridOrigin;

                if (local is { X: >= 0 and < ChunkSize, Y: >= 0 and < ChunkSize })
                    own.Add(source);
            }

            return new BuiltChunk(polys, own.ToArray());
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
        var obstacle = TryGetTagIndex(ObstacleTag, out var obstacleIndex) ? obstacleIndex : -1;
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

            if (!_fixturesQuery.TryComp(ent, out var fixtures) || !IsBodyRelevant(fixtures))
                continue;

            // A body is in all the tiles it takes up, and not only in the one its center is in: a tree is bigger
            // than a tile.
            var bounds = GetBounds(fixtures, xform, tile);
            var minTile = new Vector2i((int) MathF.Floor(bounds.Left), (int) MathF.Floor(bounds.Bottom));
            var maxTile = new Vector2i((int) MathF.Floor(bounds.Right), (int) MathF.Floor(bounds.Top));

            for (var x = Math.Max(minTile.X, gridOrigin.X); x <= Math.Min(maxTile.X, gridOrigin.X + ChunkSize - 1); x++)
            {
                for (var y = Math.Max(minTile.Y, gridOrigin.Y); y <= Math.Min(maxTile.Y, gridOrigin.Y + ChunkSize - 1); y++)
                {
                    (buckets[(x - gridOrigin.X) * ChunkSize + (y - gridOrigin.Y)] ??= new()).Add((ent, fixtures));
                }
            }

            // The tiles of the chunks next to this one that a body of the world takes up influence the tiles of this one
            // by being close to them. The chunk next to this one that is built already has them among its sources.
            if (obstacle >= 0
                && _physicsQuery.TryComp(ent, out var body)
                && body.BodyType == BodyType.Static
                && !(tags != null && MakesNoObstacle(tags.Tags)))
            {
                for (var x = Math.Max(minTile.X, (int) box.Left); x <= Math.Min(maxTile.X, (int) box.Right - 1); x++)
                {
                    for (var y = Math.Max(minTile.Y, (int) box.Bottom); y <= Math.Min(maxTile.Y, (int) box.Top - 1); y++)
                    {
                        var other = new Vector2i(x, y);
                        var inChunk = x >= gridOrigin.X && x < gridOrigin.X + ChunkSize
                                      && y >= gridOrigin.Y && y < gridOrigin.Y + ChunkSize;

                        if (!inChunk && !map.Chunks.ContainsKey(SharedMapSystem.GetChunkIndices(other, ChunkSize)))
                            sources.Add(new PathTagSource((byte) obstacle, other));
                    }
                }
            }
        }

        return buckets;
    }

    /// <summary>
    /// The region of the grid that the solid fixtures of the body take up. The tile is returned if the body has no
    /// fixtures, and it is the one the body is in.
    /// </summary>
    private static Box2 GetBounds(FixturesComponent fixtures, TransformComponent xform, Vector2i tile)
    {
        var bounds = new Box2(tile.X + 0.5f, tile.Y + 0.5f, tile.X + 0.5f, tile.Y + 0.5f);
        var found = false;
        var transform = new Transform(xform.LocalPosition, xform.LocalRotation);

        foreach (var fixture in fixtures.Fixtures.Values)
        {
            if (!fixture.Hard)
                continue;

            // The region of the shape and not the one of its proxy, which is larger than the body to be moved by.
            for (var i = 0; i < fixture.Shape.ChildCount; i++)
            {
                var region = fixture.Shape.ComputeAABB(transform, i);
                bounds = found ? bounds.Union(region) : region;
                found = true;
            }
        }

        // The edge of a region that ends on the border of a tile is not in the tile after it. The polygons of the walls are
        // a little larger than the tile they are in, by their skin.
        return found ? bounds.Enlarged(-BoundsSkin) : bounds;
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

        var obstacle = TryGetTagIndex(ObstacleTag, out var obstacleIndex) ? obstacleIndex : -1;
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

                        var entity = GetVirtualEntity(info.Entity);

                        foreach (var tag in entity.Tags)
                        {
                            sources.Add(new PathTagSource(tag, tile));
                        }

                        // What the biome puts there does not move, the tiles of this chunk are close to it.
                        if (obstacle >= 0 && !entity.Soft && entity.Layers[FootprintIndex(0, 0)] != null)
                            sources.Add(new PathTagSource((byte) obstacle, tile));
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


    /// <summary>
    /// Whether some tag of the body says that the body is not an obstacle to keep away from.
    /// </summary>
    private bool MakesNoObstacle(IEnumerable<ProtoId<PathTagPrototype>> tags)
    {
        foreach (var tag in tags)
        {
            if (TryGetTagIndex(tag, out var index) && !TagMakesObstacle(index))
                return true;
        }

        return false;
    }

    private VirtualEntity GetVirtualEntity(string proto) => _virtualEntities.GetOrAdd(proto, CreateVirtualEntity);

    private VirtualEntity CreateVirtualEntity(string protoId)
    {
        if (!_proto.TryIndex<EntityPrototype>(protoId, out var proto))
            return new VirtualEntity(new int[]?[FootprintSize], new int[]?[FootprintSize], null, Array.Empty<byte>(), false);

        var tags = new List<byte>();

        if (proto.TryComp<PathTagsComponent>(out var tagsComp, EntityManager.ComponentFactory))
        {
            foreach (var tag in tagsComp.Tags)
            {
                if (TryGetTagIndex(tag, out var index))
                    tags.Add((byte) index);
            }
        }

        // A body with such a tag is not an obstacle to keep away from, see the tag.
        var isSoft = tags.Any(tag => !TagMakesObstacle(tag));

        var layers = new int[]?[FootprintSize];
        var masks = new int[]?[FootprintSize];

        // The biome puts the entity in the center of the tile, without rotation.
        if (!proto.TryComp<FixturesComponent>(out var fixtures, EntityManager.ComponentFactory)
            || !IsBodyRelevant(fixtures))
            return new VirtualEntity(layers, masks, GetDestroyDamage(proto), tags.ToArray(), isSoft);

        var transform = new Transform(new Vector2(0.5f, 0.5f), Angle.Zero);

        for (var offsetX = -1; offsetX <= 1; offsetX++)
        {
            for (var offsetY = -1; offsetY <= 1; offsetY++)
            {
                var index = FootprintIndex(offsetX, offsetY);

                for (var subX = 0; subX < SubStep; subX++)
                {
                    for (var subY = 0; subY < SubStep; subY++)
                    {
                        var point = new Vector2(
                            offsetX + StepOffset + (float) subX / SubStep,
                            offsetY + StepOffset + (float) subY / SubStep);

                        foreach (var fixture in fixtures.Fixtures.Values)
                        {
                            if (!fixture.Hard || !_fixtures.TestPoint(fixture.Shape, transform, point))
                                continue;

                            layers[index] ??= new int[SubStep * SubStep];
                            masks[index] ??= new int[SubStep * SubStep];
                            layers[index]![subX * SubStep + subY] |= fixture.CollisionLayer;
                            masks[index]![subX * SubStep + subY] |= fixture.CollisionMask;
                        }
                    }
                }
            }
        }

        return new VirtualEntity(layers, masks, GetDestroyDamage(proto), tags.ToArray(), isSoft);
    }

    /// <summary>
    /// Applies what a virtual entity does to the points of a tile that it reaches, the tile is at the given offset from
    /// the tile of the entity.
    /// </summary>
    private static void ApplyFootprint(
        RfPathfindingData[] points,
        int tileX,
        int tileY,
        in VirtualEntity entity,
        int offsetX,
        int offsetY,
        Span<bool> indestructible,
        ref bool solid)
    {
        var index = FootprintIndex(offsetX, offsetY);

        if (entity.Layers[index] is not { } layers || entity.Masks[index] is not { } masks)
            return;

        // What the biome creates does not move. What has a tag that is not an obstacle (what can be climbed onto, for one) is not
        // kept away from, see the tag.
        solid |= !entity.Soft;

        for (var i = 0; i < SubStep * SubStep; i++)
        {
            if (layers[i] == 0 && masks[i] == 0)
                continue;

            ref var data = ref points[PointIndex(tileX, tileY, i / SubStep, i % SubStep)];
            data.CollisionLayer |= layers[i];
            data.CollisionMask |= masks[i];
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
        Span<bool> indestructible,
        ref bool solid)
    {
        var xform = Transform(ent);
        var isStatic = _physicsQuery.TryComp(ent, out var body) && body.BodyType == BodyType.Static
                       && !(_tagsQuery.TryComp(ent, out var entityTags) && MakesNoObstacle(entityTags.Tags));
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
                solid |= isStatic;
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
