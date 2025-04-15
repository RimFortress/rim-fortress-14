using System.Numerics;
using Content.Server._RF.NPC;
using Content.Server.Mind;
using Content.Server.Parallax;
using Content.Server.Preferences.Managers;
using Content.Server.Station.Systems;
using Content.Shared._RF.World;
using Content.Shared.Light.Components;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Preferences;
using Content.Shared.Random.Helpers;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
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
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IServerPreferencesManager _preferences = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly StationSpawningSystem _station = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;

    /// <summary>
    /// A queue of pending requests to spawn starting settlements
    /// </summary>
    /// <remarks>
    /// We cannot spawn them immediately, as the map has not been loaded yet,
    /// and we cannot reliably search for obstacles
    /// </remarks>
    private readonly HashSet<(EntityUid, TimeSpan)> _roundstartSpawnQueue = new();

    private EntityUid CreateMap()
    {
        if (Rule is not { } rule)
            throw new InvalidOperationException("trying create world map before rule is set");

        var map = _map.CreateMap(out var mapId);
        var templateId = _prototype.Index(rule.BiomeSet).Pick(_random);
        _biome.EnsurePlanet(map, _prototype.Index<BiomeTemplatePrototype>(templateId));

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
            ChunkSize * (rule.PlanetChunkLoadDistance + 1),
            ChunkSize * (rule.PlanetChunkLoadDistance + 1));
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

        // Time offset so that the map has time to load
        _roundstartSpawnQueue.Add((mob, _timing.CurTime + TimeSpan.FromSeconds(2f)));

        Dirty(mob, player);
    }

    /// <summary>
    /// Adds entities to the list of entities controlled by the player
    /// </summary>
    public void AddPops(Entity<RimFortressPlayerComponent?> player, List<EntityUid> pops)
    {
        if (!Resolve(player.Owner, ref player.Comp))
            return;

        foreach (var pop in pops)
        {
            var comp = EnsureComp<ControllableNpcComponent>(pop);
            comp.CanControl.Add(player);
        }

        player.Comp.Pops.AddRange(pops);
        Dirty(player);
    }

    /// <summary>
    /// Creates starting settlement for the player according to the rule,
    /// also sets their settings according to the player's preferences
    /// </summary>
    private void SpawnRoundstartPops(Entity<RimFortressPlayerComponent?> player)
    {
        if (Rule is not { } rule
            || !Resolve(player.Owner, ref player.Comp)
            || player.Comp.GotRoundstartPops
            || !_player.TryGetSessionByEntity(player, out var session))
            return;

        var prefs = _preferences.GetPreferences(session.UserId);

        foreach (var map in player.Comp.OwnedMaps)
        {
            if (!TryComp(map, out MapGridComponent? grid))
                continue;

            var area = Box2.CenteredAround(
                Transform(player).Coordinates.Position,
                new Vector2(rule.RoundStartSpawnRadius));

            var pops = new List<EntityUid>();
            var freeTiles = GetFreeTiles(map, area);

            // If we really want to spawn these entities, but we can't,
            // we remove everything that's in our way.
            if (freeTiles.Count == 0)
            {
                var tileRef = _map.GetTileRef(map, grid, new EntityCoordinates(map, area.Center));
                var box = Box2.CenteredAround(Turf.GetTileCenter(tileRef).Position, Vector2.One);

                foreach (var entity in _lookup.GetEntitiesIntersecting(map, box, LookupFlags.Static))
                {
                    EntityManager.DeleteEntity(entity);
                }

                freeTiles.Add(tileRef);
            }

            var index = 0;
            foreach (var (_, profile) in prefs.Characters)
            {
                if (index == rule.MaxRoundstartPops)
                    break;

                var coords = Turf.GetTileCenter(_random.Pick(freeTiles));
                var character = (HumanoidCharacterProfile) profile;
                var job = PickPopJob(character.JobPriorities) ?? rule.DefaultPopsJob;
                var pop = _station.SpawnPlayerMob(coords, job, character, null);

                if (rule.PopsComponentsOverride != null)
                    EntityManager.AddComponents(pop, rule.PopsComponentsOverride, false);

                pops.Add(pop);
                index++;
            }

            AddPops(player, pops);
        }

        player.Comp.GotRoundstartPops = true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        foreach (var (player, time) in _roundstartSpawnQueue)
        {
            if (time > _timing.CurTime)
                break;

            SpawnRoundstartPops(player);
            _roundstartSpawnQueue.Remove((player, time));
        }
    }
}
