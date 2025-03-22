using System.Linq;
using System.Numerics;
using Content.Server._RF.GameTicking.Rules;
using Content.Server.Mind;
using Content.Server.Parallax;
using Content.Shared.Parallax.Biomes;
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

    private MapId[,] _worlds = new MapId[0,0]; // [Y,X]
    private Dictionary<MapId, (int, int)> _worldsCoords = [];
    private List<(int, int)> _freeWorlds = [];
    private Dictionary<NetUserId, List<MapId>> _mapOwners = new();
    private RimFortressRuleComponent? _rule;

    private const byte ChunkSize = 8; // Copy of SharedBiomeSystem.ChunkSize

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
        var map = _map.CreateMap(out var mapId);
        var biomes = _protoManager.EnumeratePrototypes<BiomeTemplatePrototype>().ToList();
        _biome.EnsurePlanet(map, biomes[_random.Next(biomes.Count - 1)]);

        _worlds[y, x] = mapId;
        _worldsCoords[mapId] = (y, x);

        return mapId;
    }

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

    public void CreateOwnerMap(ICommonSession player)
    {
        if (_rule is not { } rule)
            return;

        if (_freeWorlds.Count == 0)
            throw new InvalidOperationException("No free worlds available");

        var (x, y) = _freeWorlds[_random.Next(_freeWorlds.Count - 1)];
        var mapId = CreateMap(x, y);

        // Spawn RF player entity
        var newMind = _mind.CreateMind(player.UserId, player.Name);
        _mind.SetUserId(newMind, player.UserId);
        var mob = Spawn(rule.PlayerProtoId, new MapCoordinates(new Vector2(ChunkSize / 2f), mapId));
        _mind.TransferTo(newMind, mob);

        SetMapOwner(mapId, player.UserId);

        // Build map borders around map center
        var borderBox = new Box2i(
            -ChunkSize * rule.PlanetChunkLoadDistance - 1,
            -ChunkSize * rule.PlanetChunkLoadDistance - 1,
            ChunkSize * (rule.PlanetChunkLoadDistance + 1) + 1,
            ChunkSize * (rule.PlanetChunkLoadDistance + 1) + 1);
        CreateMapBorders(rule.PlanetBorderProtoId, mapId, borderBox);
    }

    public bool IsWorldMap(MapId mapId)
    {
        return _worldsCoords.ContainsKey(mapId);
    }

    public bool ChunkInMapLimits(Vector2i indicates)
    {
        if (_rule is not { } rule)
            return true;

        return Math.Abs(indicates.X) <= rule.PlanetChunkLoadDistance * ChunkSize
               && Math.Abs(indicates.Y) <= rule.PlanetChunkLoadDistance * ChunkSize;
    }
}
