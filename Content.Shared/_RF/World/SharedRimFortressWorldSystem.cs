using System.Numerics;
using Content.Shared._RF.GameTicking.Rules;
using Content.Shared.Maps;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
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
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    [ValidatePrototypeId<TagPrototype>]
    private readonly ProtoId<TagPrototype> _factionPopTag = "PlayerFactionPop";

    private int _lastFaction = 1;

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
        {
            _faction.AddFaction((uid, null), player.Faction);
        }
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
            Spawn(bordersProtoId, new MapCoordinates(new Vector2(x, box.Left), id));
            Spawn(bordersProtoId, new MapCoordinates(new Vector2(x, box.Right), id));
        }

        for (var y = box.Left + 1; y < box.Right; y++)
        {
            Spawn(bordersProtoId, new MapCoordinates(new Vector2(box.Top, y), id));
            Spawn(bordersProtoId, new MapCoordinates(new Vector2(box.Bottom, y), id));
        }
    }

    /// <summary>
    /// Creates a faction for a player
    /// </summary>
    protected string? GetPlayerFaction(EntityUid uid)
    {
        if (Rule is not { } rule)
            return null;

        var factionId = $"{rule.FactionProtoPrefix}{_lastFaction + 1}";
        _faction.AddFaction((uid, null), factionId);
        _lastFaction++;

        EnsureComp<RimFortressPlayerComponent>(uid).Faction = factionId;

        // Add friends
        foreach (var friend in rule.PlayerFactionFriends)
        {
            _faction.MakeFriendly(factionId, friend);
            _faction.MakeFriendly(friend, factionId);
        }

        // Add hostiles
        foreach (var hostile in rule.PlayerFactionHostiles)
        {
            _faction.MakeHostile(factionId, hostile);
            _faction.MakeHostile(hostile, factionId);
        }

        return factionId;
    }

    /// <summary>
    /// Spawns entities in random free tiles of a given area
    /// </summary>
    /// <returns>List of spawned entities</returns>
    public List<EntityUid> SpawnPop(
        EntityUid gridUid,
        Box2 area,
        EntProtoId popProto,
        int amount = 1,
        bool hardSpawn = false)
    {
        var spawned = new List<EntityUid>();

        if (!TryComp(gridUid, out MapGridComponent? grid))
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

        if (freeTiles.Count == 0)
        {
            // If we really want to spawn these entities, but we can't,
            // we remove everything that's in our way.
            if (hardSpawn)
            {
                var tileRef = _map.GetTileRef(gridUid, grid, new EntityCoordinates(gridUid, area.Center));
                var box = Box2.CenteredAround(_turf.GetTileCenter(tileRef).Position, new Vector2(5f));
                var entities = _lookup.GetEntitiesIntersecting(gridUid, box, LookupFlags.Static ^ LookupFlags.Approximate);

                foreach (var entity in entities)
                {
                    EntityManager.DeleteEntity(entity);
                }

                freeTiles.Add(tileRef);
            }
            else
                return spawned;
        }

        // Spawn the entity on a random free tile
        for (var i = 0; i < amount; i++)
        {
            var spawnCoords = _turf.GetTileCenter(_random.Pick(freeTiles));
            var spawnedUid = Spawn(popProto, spawnCoords);
            spawned.Add(spawnedUid);
        }

        return spawned;
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

    /// <summary>
    /// Checks if the entity is part of the player's faction
    /// </summary>
    public bool IsPlayerFactionMember(EntityUid playerUid, EntityUid uid)
    {
        if (!PlayerQuery.TryComp(playerUid, out var player)
            || !TryComp(uid, out NpcFactionMemberComponent? comp)
            || !_faction.IsMember(new Entity<NpcFactionMemberComponent?>(uid, comp), player.Faction))
            return false;

        return true;
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

    /// <inheritdoc/>
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (Rule is not { } rule)
            return;

        var query = EntityQueryEnumerator<RimFortressPlayerComponent>();
        while (query.MoveNext(out var player))
        {
            foreach (var map in player.OwnedMaps)
            {
                if (!TryComp(map, out WorldMapComponent? comp))
                    continue;

                var radius = 5f;
                var loadDist = rule.PlanetChunkLoadDistance * ChunkSize;
                var randomBox = _random.Pick(new List<Box2>
                {
                    new(new Vector2(-loadDist, loadDist - radius), new Vector2(loadDist, loadDist)), // Top
                    new(new Vector2(-loadDist, -loadDist), new Vector2(-loadDist + radius, loadDist)), // Left
                    new(new Vector2(-loadDist, -loadDist), new Vector2(loadDist, -loadDist + radius)), // Down
                    new(new Vector2(loadDist - radius, -loadDist), new Vector2(loadDist, loadDist)), // Right
                });

                var pops = SpawnPop(map, randomBox, _random.Pick(rule.PopsProtoIds), amount: _random.Next(1, 3));
                player.Pops.AddRange(pops);
                comp.LastEventTime = _timing.CurTime;
            }
        }
    }
}
