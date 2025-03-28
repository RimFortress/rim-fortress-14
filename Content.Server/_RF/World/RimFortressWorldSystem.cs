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

    private EntityUid CreateMap()
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

        return map;
    }

    private Entity<WorldMapComponent> CreateOrGetMap(int x, int y)
    {
        if (Worlds[y, x] is { } mapUid
            && MapQuery.TryComp(Worlds[y, x], out var mapComp))
            return (mapUid, mapComp);

        var map = CreateMap();
        var worldMap = EnsureComp<WorldMapComponent>(map);
        worldMap.LastEventTime = _timing.CurTime;
        Worlds[y, x] = map;
        return (map, worldMap);
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

        // Spawn RF player entity
        var newMind = _mind.CreateMind(session.UserId, session.Name);
        _mind.SetUserId(newMind, session.UserId);
        var center = new EntityCoordinates(worldMap.Owner, new Vector2(ChunkSize / 2f));
        var mob = Spawn(rule.PlayerProtoId, center);
        _mind.TransferTo(newMind, mob);

        var player = EnsureComp<RimFortressPlayerComponent>(mob);
        player.OwnedMaps.Add(worldMap.Owner);

        if (GetPlayerFaction(mob) is { } faction)
            player.Faction = faction;

        // Spawn roundstart settlements
        var area = Box2.CenteredAround(center.Position, new Vector2(rule.RoundStartSpawnRadius));
        var pop = _random.Pick(rule.PopsProtoIds);
        var pops = SpawnPop(worldMap.Owner, area, pop, amount: rule.RoundstartPops, hardSpawn: true);
        player.Pops.AddRange(pops);
    }
}
