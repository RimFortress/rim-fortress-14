using System.Numerics;
using Content.Shared._RF.GameTicking.Rules;
using Content.Shared.Maps;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Physics;
using Content.Shared.Tag;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._RF.World;

public abstract class SharedRimFortressWorldSystem : EntitySystem
{
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    [ValidatePrototypeId<TagPrototype>]
    public readonly ProtoId<TagPrototype> FactionPopTag = "PlayerFactionPop";

    protected MapId[,] Worlds = new MapId[0, 0]; // [Y,X]
    protected RimFortressRuleComponent? Rule;

    protected readonly Dictionary<MapId, (int, int)> WorldsCoords = [];
    protected readonly List<(int, int)> FreeWorlds = [];
    protected readonly Dictionary<NetUserId, List<MapId>> MapOwners = new();
    protected readonly Dictionary<NetUserId, string> PlayerFactions = new();
    protected readonly Dictionary<NetUserId, TimeSpan> LastSpawnTime = new();
    protected readonly Dictionary<NetUserId, List<EntityUid>> PlayerPops = new();

    protected const byte ChunkSize = 8; // Copy of SharedBiomeSystem.ChunkSize

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TagComponent, MapInitEvent>(OnSpawn);
    }

    // We use RandomHumanoidSpawner to spawn pops,
    // so we can't set the faction at once, so we resort to these crutches
    protected void OnSpawn(EntityUid uid, TagComponent component, MapInitEvent args)
    {
        if (_tag.HasTag(uid, FactionPopTag)
            && GetPlayerByMap(Transform(uid).MapID) is { } player
            && PlayerFactions.TryGetValue(player, out var factionId))
        {
            _faction.AddFaction((uid, null), factionId);
        }
    }

    /// <summary>
    /// Create a world with the size specified in <see cref="RimFortressRuleComponent"/>.WorldSize
    /// </summary>
    /// <param name="rule">game rule component</param>
    public void InitializeWorld(RimFortressRuleComponent rule)
    {
        var size = rule.WorldSize;
        Worlds = new MapId[size.Y, size.X];
        FreeWorlds.Clear();
        MapOwners.Clear();
        PlayerFactions.Clear();
        LastSpawnTime.Clear();

        for (var y = 0; y < size.Y; y++)
        {
            for (var x = 0; x < size.X; x++)
            {
                Worlds[y, x] = MapId.Nullspace;
                FreeWorlds.Add((x, y));
            }
        }

        Rule = rule;
    }

    protected void SetMapOwner(MapId map, NetUserId owner)
    {
        if (!MapOwners.ContainsKey(owner))
            MapOwners[owner] = [];

        MapOwners[owner].Add(map);

        if (WorldsCoords.TryGetValue(map, out var coords))
            FreeWorlds.Remove(coords);
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
    /// Creates a faction for a player, if none exists
    /// </summary>
    protected void CreatePlayerFaction(ICommonSession player)
    {
        if (PlayerFactions.ContainsKey(player.UserId)
            || player.AttachedEntity is not { } entity
            || Rule is not { } rule)
            return;

        var factionId = $"{rule.FactionProtoPrefix}{PlayerFactions.Count + 1}";
        _faction.AddFaction((entity, null), factionId);

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

        PlayerFactions.Add(player.UserId, factionId);
    }

    public void SpawnPlayerPop(
        NetUserId userId,
        MapId mapId,
        Box2 area,
        EntProtoId popProto,
        int amount = 1,
        bool hardSpawn = false)
    {
        var pops = SpawnPop(mapId, area, popProto, amount, hardSpawn);

        if (!PlayerPops.TryGetValue(userId, out _))
            PlayerPops.Add(userId, pops);
        else
            PlayerPops[userId].AddRange(pops);
    }

    /// <summary>
    /// Spawns entities in random free tiles of a given area
    /// </summary>
    /// <returns>List of spawned entities</returns>
    public List<EntityUid> SpawnPop(
        MapId mapId,
        Box2 area,
        EntProtoId popProto,
        int amount = 1,
        bool hardSpawn = false)
    {
        var gridUid = _transform.ToCoordinates(new MapCoordinates(Vector2.Zero, mapId)).EntityId;
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
                var tileRef = _map.GetTileRef(gridUid, grid, new MapCoordinates(area.Center, mapId));
                var box = Box2.CenteredAround(_turf.GetTileCenter(tileRef).Position, Vector2.One);
                var entities = _lookup.GetEntitiesIntersecting(mapId, box, LookupFlags.Static);

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
    public bool IsWorldMap(MapId mapId)
    {
        return WorldsCoords.ContainsKey(mapId);
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
    public bool IsPlayerFactionMember(NetUserId userId, EntityUid uid)
    {
        if (!PlayerFactions.TryGetValue(userId, out var faction)
            || !TryComp(uid, out NpcFactionMemberComponent? comp)
            || !_faction.IsMember(new Entity<NpcFactionMemberComponent?>(uid, comp), faction))
            return false;

        return true;
    }

    public NetUserId? GetPlayerByMap(MapId mapId)
    {
        foreach (var (userId, maps) in MapOwners)
        {
            if (maps.Contains(mapId))
                return userId;
        }

        return null;
    }

    /// <inheritdoc/>
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (Rule is not { } rule)
            return;

        foreach (var (userId, time) in LastSpawnTime)
        {
            if (time + rule.SpawnPopDuration > _timing.CurTime
                || !MapOwners.TryGetValue(userId, out var maps))
                continue;

            foreach (var mapId in maps)
            {
                var radius = 5f;
                var loadDist = rule.PlanetChunkLoadDistance * ChunkSize;
                var randomBox = _random.Pick(new List<Box2>
                {
                    new(new Vector2(-loadDist, loadDist - radius), new Vector2(loadDist, loadDist)), // Top
                    new(new Vector2(-loadDist, -loadDist), new Vector2(-loadDist + radius, loadDist)), // Left
                    new(new Vector2(-loadDist, -loadDist), new Vector2(loadDist, -loadDist + radius)), // Down
                    new(new Vector2(loadDist - radius, -loadDist), new Vector2(loadDist, loadDist)), // Right
                });

                SpawnPlayerPop(userId, mapId, randomBox, _random.Pick(rule.PopsProtoIds), amount: _random.Next(1, 3));
            }

            LastSpawnTime[userId] = _timing.CurTime;
        }
    }
}
