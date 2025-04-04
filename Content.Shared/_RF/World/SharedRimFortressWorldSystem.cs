using System.Linq;
using System.Numerics;
using Content.Shared._RF.GameTicking.Rules;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Tag;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._RF.World;

public abstract class SharedRimFortressWorldSystem : EntitySystem
{
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    [ValidatePrototypeId<TagPrototype>]
    private readonly ProtoId<TagPrototype> _factionPopTag = "PlayerFactionPop";

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

        SubscribeLocalEvent<TagComponent, MapInitEvent>(OnSpawn);
    }

    // We use RandomHumanoidSpawner to spawn pops,
    // so we can't set the faction at once, so we resort to these crutches
    private void OnSpawn(EntityUid uid, TagComponent component, MapInitEvent args)
    {
        if (_tag.HasTag(uid, _factionPopTag)
            && GetPlayerByMap(_map.GetMap(Transform(uid).MapID)) is { } player)
            player.Pops.Add(uid);
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
        int amount = 1,
        bool hardSpawn = false)
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

        return SpawnPop(gridUid, _random.Pick(chunks), popProto, amount, hardSpawn);
    }

    /// <summary>
    /// Spawns entities in random free tiles, connected to the map border, of a given area
    /// </summary>
    /// <returns>List of spawned entities</returns>
    public List<EntityUid> SpawnPop(
        EntityUid gridUid,
        Box2 area,
        EntProtoId? popProto = null,
        int amount = 1,
        bool hardSpawn = false)
    {
        var spawned = new List<EntityUid>();

        if (!TryComp(gridUid, out MapGridComponent? grid)
            || Rule is not { } rule)
            return spawned;

        var tileEnumerator = _map.GetTilesEnumerator(gridUid, grid, area);
        var freeTiles = new List<TileRef>();

        // Find all free tiles in the specified area
        while (tileEnumerator.MoveNext(out var tileRef))
        {
            if (_turf.IsTileBlocked(tileRef, CollisionGroup.Impassable ^ CollisionGroup.HighImpassable))
                continue;

            freeTiles.Add(tileRef);
        }

        while (freeTiles.Count > 0)
        {
            if (amount == 0)
                return spawned;

            var randomTile = _random.Pick(freeTiles);
            freeTiles.Remove(randomTile);

            if (IsConnectedToBorder(randomTile, Rule.PlanetBorderProtoId, out var tiles))
            {
                freeTiles = freeTiles.Where(tile => tiles.Contains(tile)).ToList();
                break;
            }

            foreach (var visited in tiles)
            {
                freeTiles.Remove(visited);
            }
        }

        // If we really want to spawn these entities, but we can't,
        // we remove everything that's in our way.
        if (freeTiles.Count == 0 && hardSpawn)
        {
            var tileRef = _map.GetTileRef(gridUid, grid, new EntityCoordinates(gridUid, area.Center));
            var box = Box2.CenteredAround(_turf.GetTileCenter(tileRef).Position, Vector2.One);
            var entities = _lookup.GetEntitiesIntersecting(gridUid, box, LookupFlags.Static);

            foreach (var entity in entities)
            {
                EntityManager.DeleteEntity(entity);
            }

            freeTiles.Add(tileRef);
        }

        if (freeTiles.Count == 0)
            return spawned;

        // Spawn the entities on a random free tile
        while (amount > 0)
        {
            var spawnCoords = _turf.GetTileCenter(_random.Pick(freeTiles));
            var protoId = popProto;

            if (popProto == null)
                protoId = _random.Pick(rule.PopsProtoIds);

            var spawnedUid = Spawn(protoId, spawnCoords);
            spawned.Add(spawnedUid);
            amount--;
        }

        return spawned;
    }

    /// <summary>
    /// Checks if the tile is connected to the map border
    /// </summary>
    /// <param name="tileRef">tile from which the check will start</param>
    /// <param name="border">border entity prototype id</param>
    /// <param name="visited">list of visited tiles</param>
    protected bool IsConnectedToBorder(TileRef tileRef, EntProtoId border, out HashSet<TileRef> visited)
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

            if (_turf.IsTileBlocked(node, CollisionGroup.Impassable ^ CollisionGroup.HighImpassable))
                continue;

            // NOTE: space is all unloaded map tiles, even if they are not map boundaries.
            if (node.IsSpace())
                return true;

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

    public RimFortressPlayerComponent? GetPlayerByMap(EntityUid mapId)
    {
        var query = EntityQueryEnumerator<RimFortressPlayerComponent>();
        while (query.MoveNext(out var player))
        {
            if (player.OwnedMaps.Contains(mapId))
                return player;
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
