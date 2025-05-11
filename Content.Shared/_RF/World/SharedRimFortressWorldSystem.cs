using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Content.Shared._RF.GameTicking.Rules;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared._RF.World;

public abstract class SharedRimFortressWorldSystem : EntitySystem
{
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] protected readonly TurfSystem Turf = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    protected RimFortressRuleComponent? Rule;
    protected EntityUid?[,] Worlds = new EntityUid?[0, 0]; // [Y,X]

    protected const byte ChunkSize = 8; // Copy of SharedBiomeSystem.ChunkSize

    protected EntityQuery<WorldMapComponent> MapQuery;
    protected EntityQuery<RimFortressPlayerComponent> PlayerQuery;

    public override void Initialize()
    {
        base.Initialize();

        MapQuery = GetEntityQuery<WorldMapComponent>();
        PlayerQuery = GetEntityQuery<RimFortressPlayerComponent>();
    }

    /// <summary>
    /// Create a world with the size specified in <see cref="RimFortressRuleComponent"/>.WorldSize
    /// </summary>
    /// <param name="rule">game rule component</param>
    public void InitializeWorld(RimFortressRuleComponent rule)
    {
        var size = rule.WorldSize;
        Worlds = new EntityUid?[size.Y, size.X];

        for (var y = 0; y < size.Y; y++)
        {
            for (var x = 0; x < size.X; x++)
            {
                Worlds[y, x] = null;
            }
        }

        Rule = rule;
    }

    /// <summary>
    /// Creates a box of entities of a given prototype
    /// </summary>
    /// <param name="bordersProtoId">prototype from which the border will be constructed</param>
    /// <param name="id">id of the map on which the border will be created</param>
    /// <param name="box">border box</param>
    protected void CreateMapBorders(string bordersProtoId, MapId id, Box2i box)
    {
        for (var x = box.Bottom; x < box.Top; x++)
        {
            Spawn(bordersProtoId, new MapCoordinates(new Vector2(x, box.Left) + new Vector2(0.5f), id));
            Spawn(bordersProtoId, new MapCoordinates(new Vector2(x, box.Right) + new Vector2(0.5f), id));
        }

        for (var y = box.Left + 1; y < box.Right; y++)
        {
            Spawn(bordersProtoId, new MapCoordinates(new Vector2(box.Top, y) + new Vector2(0.5f), id));
            Spawn(bordersProtoId, new MapCoordinates(new Vector2(box.Bottom, y) + new Vector2(0.5f), id));
        }
    }

    public List<EntityUid> SpawnPopAlongBounds(
        EntityUid gridUid,
        int spawnChunks,
        EntProtoId? popProto = null,
        int amount = 1)
    {
        if (Rule is not { } rule)
            return new List<EntityUid>();

        var chunkSize = spawnChunks * ChunkSize;
        var ind = rule.PlanetChunkLoadDistance / spawnChunks;
        var chunks = new List<Box2>();

        for (var x = -ind; x < ind; x++)
        {
            chunks.Add(Box2.CenteredAround(new Vector2(x * chunkSize, ind * chunkSize), new Vector2(chunkSize)));
            chunks.Add(Box2.CenteredAround(new Vector2(x * chunkSize, -ind * chunkSize), new Vector2(chunkSize)));
        }

        for (var y = -ind + 1; y < ind - 1; y++)
        {
            chunks.Add(Box2.CenteredAround(new Vector2(ind * chunkSize, y * chunkSize), new Vector2(chunkSize)));
            chunks.Add(Box2.CenteredAround(new Vector2(-ind * chunkSize, y * chunkSize), new Vector2(chunkSize)));
        }

        return SpawnPop(gridUid, _random.Pick(chunks), popProto, amount);
    }

    /// <summary>
    /// Spawns entities in random free tiles, connected to the map border, of a given area
    /// </summary>
    /// <param name="gridUid">Grid on which to spawn entities</param>
    /// <param name="area">Area within which spawning can occur</param>
    /// <param name="popProto">Prototype of the entity to be spawned</param>
    /// <param name="amount">Amount of entities to be spawned</param>
    /// <param name="entities">Entities spawned elsewhere previously that will be used in place of the prototype spawning</param>
    /// <returns>List of spawned entities</returns>
    public List<EntityUid> SpawnPop(
        EntityUid gridUid,
        Box2 area,
        EntProtoId? popProto = null,
        int amount = 1,
        List<EntityUid>? entities = null)
    {
        DebugTools.Assert(popProto != null || entities?.Count == amount);

        var spawned = new List<EntityUid>();
        var freeTiles = GetFreeTiles(gridUid, area);

        if (freeTiles.Count == 0)
            return spawned;

        // Spawn the entities on a random free tile
        while (amount > 0)
        {
            var spawnCoords = Turf.GetTileCenter(_random.Pick(freeTiles));

            // If we already have entities ready to go, we simply move them to the free places
            if (entities?.Count > 0)
            {
                var entity = entities.Pop();
                _transform.AttachToGridOrMap(entity);
                _transform.SetCoordinates(entity, spawnCoords);
                amount--;
                continue;
            }

            var spawnedUid = Spawn(popProto, spawnCoords);
            spawned.Add(spawnedUid);
            amount--;
        }

        return spawned;
    }

    protected HashSet<TileRef> GetFreeTiles(Entity<MapGridComponent?> grid, Box2 area)
    {
        if (!Resolve(grid, ref grid.Comp))
            return new();

        var tileEnumerator = _map.GetTilesEnumerator(grid, grid.Comp, area);
        var freeTiles = new HashSet<TileRef>();

        // Find all free tiles in the specified area
        while (tileEnumerator.MoveNext(out var tileRef))
        {
            // We also check entities with non-hard fixtures to avoid spawning on entities like water, lava, chasms.
            // It's obviously not the best solution to interfere with the TurfSystem code,
            // a better solution would be to have some list of entities on which we can't spawn entities
            // and check the tile for those entities, but then we'd have to write own version of IsTileBlocked
            // to avoid calling GetEntitiesIntersecting repeatedly from EntityLookupSystem
            if (Turf.IsTileBlocked(tileRef, CollisionGroup.Impassable ^ CollisionGroup.HighImpassable))
                continue;

            freeTiles.Add(tileRef);
        }

        while (freeTiles.Count > 0)
        {
            var randomTile = _random.Pick(freeTiles);
            freeTiles.Remove(randomTile);

            if (IsConnectedToBorder(randomTile, freeTiles, out var tiles))
            {
                freeTiles = freeTiles.Where(tile => tiles.Contains(tile)).ToHashSet();
                break;
            }

            foreach (var visited in tiles)
            {
                freeTiles.Remove(visited);
            }
        }

        return freeTiles;
    }

    /// <summary>
    /// Checks if the tile is connected to the map border
    /// </summary>
    /// <param name="tileRef">tile from which the check will start</param>
    /// <param name="freeTilesCache">A list of free tiles that have been checked before, to prevent double-checking</param>
    /// <param name="visited">list of visited tiles</param>
    protected bool IsConnectedToBorder(TileRef tileRef, HashSet<TileRef> freeTilesCache, out HashSet<TileRef> visited)
    {
        var directions = new[] { Vector2i.Left, Vector2i.Right, Vector2i.Up, Vector2i.Down };
        var queue = new Queue<TileRef>();

        queue.Enqueue(tileRef);
        visited = new();

        var gridUid = tileRef.GridUid;
        var gridComp = Comp<MapGridComponent>(gridUid);

        while (queue.TryDequeue(out var node))
        {
            if (!visited.Add(node))
                continue;

            // NOTE: space is all unloaded map tiles, even if they are not map boundaries.
            if (node.IsSpace())
                return true;

            if (!freeTilesCache.Contains(node)
                && Turf.IsTileBlocked(node, CollisionGroup.Impassable ^ CollisionGroup.HighImpassable))
                continue;

            foreach (var offset in directions)
            {
                var nextTile = _map.GetTileRef((gridUid, gridComp), node.GridIndices + offset);
                queue.Enqueue(nextTile);
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if the given map is part of the RimFortress world
    /// </summary>
    public bool IsWorldMap(EntityUid uid)
    {
        return MapQuery.HasComp(uid);
    }

    /// <summary>
    /// Checks if the coordinates are within the map borders
    /// </summary>
    public bool InMapLimits(Vector2i coordinates)
    {
        if (Rule is not { } rule)
            return true;

        return Math.Abs(coordinates.X) <= rule.PlanetChunkLoadDistance * ChunkSize
               && Math.Abs(coordinates.Y) <= rule.PlanetChunkLoadDistance * ChunkSize;
    }

    public Entity<RimFortressPlayerComponent>? GetPlayerByMap(EntityUid mapId)
    {
        var query = EntityQueryEnumerator<RimFortressPlayerComponent>();
        while (query.MoveNext(out var uid, out var player))
        {
            if (player.OwnedMaps.Contains(mapId))
                return (uid, player);
        }

        return null;
    }

    protected Vector2i? GetRandomFreeMap()
    {
        if (Rule is not { } rule)
            return null;

        var size = rule.WorldSize;
        var freeWorlds = new List<Vector2i>();

        for (var y = 0; y < size.Y; y++)
        {
            for (var x = 0; x < size.X; x++)
            {
                if (Worlds[y, x] is not { } worldMap
                    || MapQuery.TryComp(worldMap, out var map)
                    && map.OwnerPlayer == null)
                    freeWorlds.Add(new Vector2i(x, y));
            }
        }

        if (freeWorlds.Count == 0)
            return null;

        return _random.Pick(freeWorlds);
    }

    public Entity<WorldMapComponent>? GetRandomPlayerMap()
    {
        var freeWorlds = new List<EntityUid>();

        var query = EntityQueryEnumerator<RimFortressPlayerComponent>();
        while (query.MoveNext(out var player))
        {
            freeWorlds.AddRange(player.OwnedMaps);
        }

        if (freeWorlds.Count == 0)
            return null;

        var map = _random.Pick(freeWorlds);
        if (!MapQuery.TryComp(map, out var mapComp))
            return null;

        return (map, mapComp);
    }

    public List<EntityUid>? GetPLayerPops(EntityUid uid)
    {
        if (!TryComp(uid, out RimFortressPlayerComponent? player))
            return null;

        return player.Pops.Count == 0 ? null : player.Pops;
    }

    public ProtoId<JobPrototype>? PickPopJob(IReadOnlyDictionary<ProtoId<JobPrototype>, JobPriority> jobPriorities)
    {
        if (TryPick(JobPriority.High, out var picked))
            return picked;

        if (TryPick(JobPriority.Medium, out picked))
            return picked;

        if (TryPick(JobPriority.Low, out picked))
            return picked;

        return null;

        bool TryPick(JobPriority priority, [NotNullWhen(true)] out ProtoId<JobPrototype>? jobId)
        {
            var filtered = jobPriorities
                .Where(p => p.Value == priority)
                .Select(p => p.Key)
                .ToList();

            if (filtered.Count != 0)
            {
                jobId = _random.Pick(filtered);
                return true;
            }

            jobId = null;
            return false;
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (Rule is not { } rule)
            return;

        var query = EntityQueryEnumerator<WorldMapComponent>();
        while (query.MoveNext(out var uid, out var mapComp))
        {
            var entity = new Entity<WorldMapComponent>(uid, mapComp);

            if (_timing.CurTime < mapComp.NextEventTime
                || mapComp.OwnerPlayer == null)
                continue;

            entity.Comp.NextEventTime = _timing.CurTime + TimeSpan.FromSeconds(rule.MinMaxEventTiming.Next(_random));
            RaiseLocalEvent(new WorldMapAvailableForEvent { Map = entity });
        }
    }
}

public sealed class WorldMapAvailableForEvent : HandledEntityEventArgs
{
    public Entity<WorldMapComponent> Map { get; set; }
}
