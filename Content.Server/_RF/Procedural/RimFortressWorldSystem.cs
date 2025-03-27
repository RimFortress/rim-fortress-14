using System.Numerics;
using Content.Server._RF.GameTicking.Rules;
using Content.Server.Mind;
using Content.Server.Parallax;
using Content.Shared.Light.Components;
using Content.Shared.Maps;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Physics;
using Content.Shared.Random.Helpers;
using Content.Shared.Tag;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._RF.Procedural;

/// <summary>
/// Manages the RimFortress world and player maps
/// </summary>
public sealed class RimFortressWorldSystem : EntitySystem
{
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly BiomeSystem _biome = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    private MapId[,] _worlds = new MapId[0, 0]; // [Y,X]
    private RimFortressRuleComponent? _rule;

    private readonly Dictionary<MapId, (int, int)> _worldsCoords = [];
    private readonly List<(int, int)> _freeWorlds = [];
    private readonly Dictionary<NetUserId, List<MapId>> _mapOwners = new();
    private readonly Dictionary<NetUserId, string> _playerFactions = new();
    private readonly Dictionary<NetUserId, TimeSpan> _lastSpawnTime = new();
    private readonly Dictionary<NetUserId, List<EntityUid>> _playerPops = new();

    private const byte ChunkSize = 8; // Copy of SharedBiomeSystem.ChunkSize

    [ValidatePrototypeId<TagPrototype>]
    public readonly ProtoId<TagPrototype> FactionPopTag = "PlayerFactionPop";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TagComponent, MapInitEvent>(OnSpawn);
    }

    // We use RandomHumanoidSpawner to spawn pops,
    // so we can't set the faction at once, so we resort to these crutches
    private void OnSpawn(EntityUid uid, TagComponent component, MapInitEvent args)
    {
        if (_tag.HasTag(uid, FactionPopTag)
            && GetPlayerByMap(Transform(uid).MapID) is { } player
            && _playerFactions.TryGetValue(player, out var factionId))
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
        _worlds = new MapId[size.Y, size.X];
        _freeWorlds.Clear();
        _mapOwners.Clear();
        _playerFactions.Clear();
        _lastSpawnTime.Clear();

        for (var y = 0; y < size.Y; y++)
        {
            for (var x = 0; x < size.X; x++)
            {
                _worlds[y, x] = MapId.Nullspace;
                _freeWorlds.Add((x, y));
            }
        }

        _rule = rule;
    }

    private void SetMapOwner(MapId map, NetUserId owner)
    {
        if (!_mapOwners.ContainsKey(owner))
            _mapOwners[owner] = [];

        _mapOwners[owner].Add(map);

        if (_worldsCoords.TryGetValue(map, out var coords))
            _freeWorlds.Remove(coords);
    }

    private MapId CreateMap(int x, int y)
    {
        if (_rule is not { } rule)
            throw new InvalidOperationException("trying create world map before rule is set");

        var map = _map.CreateMap(out var mapId);
        var templateId = _protoManager.Index(rule.BiomeSet).Pick(_random);
        _biome.EnsurePlanet(map, _protoManager.Index<BiomeTemplatePrototype>(templateId));

        if (TryComp(map, out LightCycleComponent? cycle))
        {
            cycle.Duration = rule.DayDuration;
            cycle.Offset = rule.DayDuration / 3; // For roundstart day time
            cycle.InitialOffset = false;
        }

        // Build map borders around map center
        var borderBox = new Box2i(
            -ChunkSize * rule.PlanetChunkLoadDistance - 1,
            -ChunkSize * rule.PlanetChunkLoadDistance - 1,
            ChunkSize * (rule.PlanetChunkLoadDistance + 1) + 1,
            ChunkSize * (rule.PlanetChunkLoadDistance + 1) + 1);
        CreateMapBorders(rule.PlanetBorderProtoId, mapId, borderBox);

        _worlds[y, x] = mapId;
        _worldsCoords[mapId] = (y, x);

        return mapId;
    }

    private MapId CreateOrGetMap(int x, int y)
    {
        if (_worlds[y, x] != MapId.Nullspace)
            return _worlds[y, x];

        return CreateMap(x, y);
    }

    /// <summary>
    /// Creates a box of entities of a given prototype
    /// </summary>
    /// <param name="bordersProtoId">prototype from which the border will be constructed</param>
    /// <param name="id">id of the map on which the border will be created</param>
    /// <param name="box">border box</param>
    private void CreateMapBorders(string bordersProtoId, MapId id, Box2i box)
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
    private void CreatePlayerFaction(ICommonSession player)
    {
        if (_playerFactions.ContainsKey(player.UserId)
            || player.AttachedEntity is not { } entity
            || _rule is not { } rule)
            return;

        var factionId = $"{rule.FactionProtoPrefix}{_playerFactions.Count + 1}";
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

        _playerFactions.Add(player.UserId, factionId);
    }

    private void SpawnPlayerPop(
        NetUserId userId,
        MapId mapId,
        Box2 area,
        EntProtoId popProto,
        int amount = 1,
        bool hardSpawn = false)
    {
        var pops = SpawnPop(mapId, area, popProto, amount, hardSpawn);

        if (!_playerPops.TryGetValue(userId, out _))
            _playerPops.Add(userId, pops);
        else
            _playerPops[userId].AddRange(pops);
    }

    /// <summary>
    /// Spawns entities in random free tiles of a given area
    /// </summary>
    /// <returns>List of spawned entities</returns>
    private List<EntityUid> SpawnPop(
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
    /// Creates or allocates a free map for the player
    /// </summary>
    /// <exception cref="InvalidOperationException">if there are no maps available</exception>
    public void CreateOwnerMap(ICommonSession player)
    {
        if (_rule is not { } rule)
            return;

        if (_freeWorlds.Count == 0)
            throw new InvalidOperationException("No free maps available");

        // Create or get map for player
        var (x, y) = _freeWorlds[_random.Next(_freeWorlds.Count - 1)];
        var mapId = CreateOrGetMap(x, y);
        SetMapOwner(mapId, player.UserId);

        // Spawn RF player entity
        var newMind = _mind.CreateMind(player.UserId, player.Name);
        _mind.SetUserId(newMind, player.UserId);
        var center = new MapCoordinates(new Vector2(ChunkSize / 2f), mapId);
        var mob = Spawn(rule.PlayerProtoId, center);
        _mind.TransferTo(newMind, mob);
        CreatePlayerFaction(player);

        // Spawn roundstart settlements
        var area = Box2.CenteredAround(center.Position, new Vector2(rule.RoundStartSpawnRadius));
        SpawnPlayerPop(
            player.UserId,
            mapId,
            area,
            _random.Pick(rule.PopsProtoIds),
            amount: rule.RoundstartPops,
            hardSpawn: true);

        _lastSpawnTime[player.UserId] = _timing.CurTime;
    }

    /// <summary>
    /// Checks if the given map is part of the RimFortress world
    /// </summary>
    public bool IsWorldMap(MapId mapId)
    {
        return _worldsCoords.ContainsKey(mapId);
    }

    /// <summary>
    /// Checks if the coordinates are within the map borders
    /// </summary>
    public bool InMapLimits(Vector2i coordinates)
    {
        if (_rule is not { } rule)
            return true;

        return Math.Abs(coordinates.X) <= rule.PlanetChunkLoadDistance * ChunkSize
               && Math.Abs(coordinates.Y) <= rule.PlanetChunkLoadDistance * ChunkSize;
    }

    /// <summary>
    /// Checks if the entity is part of the player's faction
    /// </summary>
    public bool IsPlayerFactionMember(NetUserId userId, EntityUid uid)
    {
        if (!_playerFactions.TryGetValue(userId, out var faction)
            || !TryComp(uid, out NpcFactionMemberComponent? comp)
            || !_faction.IsMember(new Entity<NpcFactionMemberComponent?>(uid, comp), faction))
            return false;

        return true;
    }

    public NetUserId? GetPlayerByMap(MapId mapId)
    {
        foreach (var (userId, maps) in _mapOwners)
        {
            if (maps.Contains(mapId))
                return userId;
        }

        return null;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_rule is not { } rule)
            return;

        foreach (var (userId, time) in _lastSpawnTime)
        {
            if (time + rule.SpawnPopDuration > _timing.CurTime
                || !_mapOwners.TryGetValue(userId, out var maps))
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

            _lastSpawnTime[userId] = _timing.CurTime;
        }
    }
}
