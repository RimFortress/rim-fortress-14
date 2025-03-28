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
    [Dependency] private readonly GameTiming _timing = default!;

    private MapId CreateMap()
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

        return mapId;
    }

    private WorldMap CreateOrGetMap(int x, int y)
    {
        if (Worlds[y, x] != MapId.Nullspace
            && Maps.TryGetValue(Worlds[y, x], out var map))
            return map;

        var world = new WorldMap(CreateMap(), new Vector2(x, y));
        world.LastEventTime = _timing.CurTime;
        Worlds[y, x] = world.MapId;
        Maps[world.MapId] = world;
        return world;
    }

    /// <summary>
    /// Creates or allocates a free map for the player
    /// </summary>
    /// <exception cref="InvalidOperationException">if there are no maps available</exception>
    public void CreateOwnerMap(ICommonSession session)
    {
        if (Rule is not { } rule)
            return;

        if (GetRandomFreeMap() is not { } mapCoords)
            throw new InvalidOperationException("No free maps available");

        // Create or get map for player
        var worldMap = CreateOrGetMap(mapCoords.X, mapCoords.Y);
        worldMap.Owner = session.UserId;

        // Spawn RF player entity
        var newMind = _mind.CreateMind(session.UserId, session.Name);
        _mind.SetUserId(newMind, session.UserId);
        var center = new MapCoordinates(new Vector2(ChunkSize / 2f), worldMap.MapId);
        var mob = Spawn(rule.PlayerProtoId, center);
        _mind.TransferTo(newMind, mob);

        if (GetPlayerFaction(session.UserId, mob) is not { } faction)
            return;

        var player = new RfPlayer(session.UserId, faction);
        player.OwnedMaps.Add(worldMap);
        Players.Add(session.UserId, player);

        // Spawn roundstart settlements
        var area = Box2.CenteredAround(center.Position, new Vector2(rule.RoundStartSpawnRadius));
        var pop = _random.Pick(rule.PopsProtoIds);
        player.SpawnPop(this, worldMap, area, pop, amount: rule.RoundstartPops, hardSpawn: true);
    }
}
