using System.Numerics;
using Content.Server._RF.GameTicking.Rules;
using Content.Server.Mind;
using Content.Server.Parallax;
using Content.Shared.Light.Components;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Random.Helpers;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

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

    private MapId[,] _worlds = new MapId[0,0]; // [Y,X]
    private RimFortressRuleComponent? _rule;

    private readonly Dictionary<MapId, (int, int)> _worldsCoords = [];
    private readonly List<(int, int)> _freeWorlds = [];
    private readonly Dictionary<NetUserId, List<MapId>> _mapOwners = new();
    private readonly Dictionary<NetUserId, string> _playerFactions = new();

    private const byte ChunkSize = 8; // Copy of SharedBiomeSystem.ChunkSize

    /// <summary>
    /// Create a world with the size specified in <see cref="RimFortressRuleComponent"/>.WorldSize
    /// </summary>
    /// <param name="rule">game rule component</param>
    public void InitializeWorld(RimFortressRuleComponent rule)
    {
        var size = rule.WorldSize;
        _worlds = new MapId[size.Y, size.X];

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
        _faction.AddFaction(new Entity<NpcFactionMemberComponent?>(entity, null), factionId);

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

        var (x, y) = _freeWorlds[_random.Next(_freeWorlds.Count - 1)];
        var mapId = CreateOrGetMap(x, y);

        // Spawn RF player entity
        var newMind = _mind.CreateMind(player.UserId, player.Name);
        _mind.SetUserId(newMind, player.UserId);
        var mob = Spawn(rule.PlayerProtoId, new MapCoordinates(new Vector2(ChunkSize / 2f), mapId));
        _mind.TransferTo(newMind, mob);

        SetMapOwner(mapId, player.UserId);
        CreatePlayerFaction(player);
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
}
