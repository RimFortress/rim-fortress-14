using System.Numerics;
using Content.Server.Mind;
using Content.Server.Parallax;
using Content.Shared._RF.World;
using Content.Shared.Light.Components;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Random.Helpers;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._RF.World;

/// <summary>
/// Manages the RimFortress world and player maps
/// </summary>
public sealed class RimFortressWorldSystem : SharedRimFortressWorldSystem
{
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly BiomeSystem _biome = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private MapId CreateMap(int x, int y)
    {
        if (Rule is not { } rule)
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

        Worlds[y, x] = mapId;
        WorldsCoords[mapId] = (y, x);

        return mapId;
    }

    private MapId CreateOrGetMap(int x, int y)
    {
        if (Worlds[y, x] != MapId.Nullspace)
            return Worlds[y, x];

        return CreateMap(x, y);
    }

    /// <summary>
    /// Creates or allocates a free map for the player
    /// </summary>
    /// <exception cref="InvalidOperationException">if there are no maps available</exception>
    public void CreateOwnerMap(ICommonSession player)
    {
        if (Rule is not { } rule)
            return;

        if (FreeWorlds.Count == 0)
            throw new InvalidOperationException("No free maps available");

        // Create or get map for player
        var (x, y) = FreeWorlds[_random.Next(FreeWorlds.Count - 1)];
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

        LastSpawnTime[player.UserId] = _timing.CurTime;
    }
}
