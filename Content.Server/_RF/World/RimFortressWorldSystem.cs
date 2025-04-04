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
            cycle.MinLightLevel = 1f;
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

    private Entity<WorldMapComponent> CreateOrGetMap(Vector2i coords)
    {
        var (x, y) = coords;

        if (Worlds[y, x] is { } mapUid
            && MapQuery.TryComp(Worlds[y, x], out var mapComp))
            return (mapUid, mapComp);

        var map = CreateMap();
        var worldMap = EnsureComp<WorldMapComponent>(map);
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
        var worldMap = CreateOrGetMap(mapCoords);
        worldMap.Comp.NextEventTime = _timing.CurTime + rule.MinimumTimeUntilFirstEvent;

        // Spawn RF player entity
        var newMind = _mind.CreateMind(session.UserId, session.Name);
        _mind.SetUserId(newMind, session.UserId);
        var center = new EntityCoordinates(worldMap.Owner, new Vector2(ChunkSize / 2f));
        var mob = Spawn(rule.PlayerProtoId, center);
        _mind.TransferTo(newMind, mob);

        var player = EnsureComp<RimFortressPlayerComponent>(mob);
        player.OwnedMaps.Add(worldMap.Owner);

        worldMap.Comp.OwnerPlayer = mob;
    }
}
