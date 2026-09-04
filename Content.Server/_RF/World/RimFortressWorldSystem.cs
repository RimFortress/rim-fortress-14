using System.Linq;
using System.Numerics;
using Content.Server._RF.Equipment;
using Content.Server._RF.NPC.Executable.Systems;
using Content.Server._RF.Parallax.Fog;
using Content.Server.Administration.Managers;
using Content.Server.Mind;
using Content.Server.Parallax;
using Content.Server.Preferences.Managers;
using Content.Server.Station.Systems;
using Content.Shared._RF.GameTicking.Rules;
using Content.Shared._RF.World;
using Content.Shared._RF.CCVar;
using Content.Shared._RF.NPC.Systems;
using Content.Shared._RF.Parallax.Fog;
using Content.Shared._RF.World.Components;
using Content.Shared.Administration;
using Content.Shared.Light.Components;
using Content.Shared.Pinpointer;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._RF.World;

/// <summary>
/// Manages the RimFortress world and player maps
/// </summary>
public sealed partial class RimFortressWorldSystem : SharedRimFortressWorldSystem
{
    [Dependency] private IAdminManager _admin = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private BiomeSystem _biome = default!;
    [Dependency] private MapSystem _map = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IServerPreferencesManager _preferences = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private StationSpawningSystem _station = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private IConfigurationManager _cvar = default!;
    [Dependency] private IPlayerEquipmentManager _equipment = default!;
    [Dependency] private ExecutableGoalSystem _executable = default!;
    [Dependency] private FogOfWarSystem _faw = default!;
    [Dependency] private OwnershipSystem _ownership = default!;

    private readonly HashSet<ICommonSession> _debugSubscribers = new();

    [SubscribeNetworkEvent]
    private void OnDebugRequest(WorldDebugInfoRequest msg, EntitySessionEventArgs args)
    {
        if (!_admin.HasAdminFlag(args.SenderSession, AdminFlags.Debug))
        {
            _debugSubscribers.Remove(args.SenderSession);
            return;
        }

        if (_debugSubscribers.Add(args.SenderSession))
            return;

        _debugSubscribers.Remove(args.SenderSession);
    }

    public EntityUid InitializeWorld(EntityUid uid, RimFortressRuleComponent rule)
    {
        Rule = rule;
        var map = _map.CreateMap();
        _biome.EnsurePlanet(map, _prototype.Index(rule.Biome));

        if (TryComp(map, out LightCycleComponent? cycle))
        {
            cycle.Duration = rule.DayDuration;
            cycle.Offset = rule.DayDuration / 3; // For roundstart day time
            cycle.InitialOffset = false;
            cycle.MinLightLevel = 1f;
        }

        EnsureComp<FogOfWarComponent>(map);
        EnsureComp<WorldMapComponent>(map);

        rule.WorldMap = map;
        Dirty(uid, rule);
        return map;
    }

    /// <summary>
    /// Creates or allocates a free map for the player
    /// </summary>
    public void SpawnPlayer(ICommonSession session)
    {
        if (Rule is not { } rule)
            return;

        var coords = Turf.GetTileCenter(GetSpawnTiles(1).First());
        var spawnBox = Box2.CenteredAround(coords.Position, new Vector2(SpawnAreaRadius));
        var freeTiles = GetFreeTiles(rule.WorldMap, spawnBox, MinSpawnAreaTiles);

        if (freeTiles.Count == 0)
            return;

        // Spawn RF player entity
        var newMind = _mind.CreateMind(session.UserId, session.Name);
        _mind.SetUserId(newMind, session.UserId);

        var mob = Spawn(rule.PlayerProtoId, coords);
        _mind.TransferTo(newMind, mob);

        var player = EnsureComp<RimFortressPlayerComponent>(mob);
        player.FactionColor = new Color(_random.NextFloat(), _random.NextFloat(), _random.NextFloat());

        RoundstartSpawn(new(mob, player), freeTiles);

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
            _executable.AddController(player.Owner, pop);

            var beacon = EnsureComp<NavMapBeaconComponent>(pop);
            beacon.Color = player.Comp.FactionColor;
            beacon.Text = MetaData(pop).EntityName;

            _faw.AddFogClearer(pop, player);
            _ownership.AddOwnership(pop, owner: player);
        }

        player.Comp.Pops.AddRange(pops);
        Dirty(player);
    }

    /// <summary>
    /// Adds entity to the list of entities controlled by the player
    /// </summary>
    public void AddPop(Entity<RimFortressPlayerComponent?> player, EntityUid pop)
    {
        if (!Resolve(player.Owner, ref player.Comp))
            return;

        _executable.AddController(player.Owner, pop);

        var beacon = EnsureComp<NavMapBeaconComponent>(pop);
        beacon.Color = player.Comp.FactionColor;
        beacon.Text = MetaData(pop).EntityName;

        _faw.AddFogClearer(pop, player);
        _ownership.AddOwnership(pop, owner: player);

        player.Comp.Pops.Add(pop);
        Dirty(player);
    }

    /// <summary>
    /// Removes an entity from the list of pops under the player's controller
    /// </summary>
    /// <param name="player">Player</param>
    /// <param name="pop">Entity to remove</param>
    /// <param name="removeLast">If true, the last pop can be removed</param>
    public bool RemovePop(Entity<RimFortressPlayerComponent?> player, EntityUid pop, bool removeLast = false)
    {
        if (!Resolve(player.Owner, ref player.Comp))
            return false;

        if (!removeLast && player.Comp.Pops.Count <= 1)
            return false;

        if (!player.Comp.Pops.Remove(pop))
            return false;

        _executable.RemoveController(player.Owner, pop);
        RemComp<NavMapBeaconComponent>(pop);

        _faw.RemoveFogClearer(pop, player);

        Dirty(player);
        return true;
    }

    /// <summary>
    /// Spawns starting pops and expedition equipment for the player
    /// </summary>
    /// <remarks>
    /// The number of spawned pops cannot be greater than <see cref="RfVars.MaxRoundstartPops"/>
    /// </remarks>
    private void RoundstartSpawn(Entity<RimFortressPlayerComponent?> player, HashSet<TileRef> spawnTiles)
    {
        if (Rule is not { } rule
            || !Resolve(player.Owner, ref player.Comp)
            || player.Comp.GotRoundstartPops
            || !_player.TryGetSessionByEntity(player, out var session))
            return;

        var pops = new List<EntityUid>();
        var prefs = _preferences.GetPreferences(session.UserId);
        var grid = Comp<MapGridComponent>(rule.WorldMap);
        var playerCoords = Transform(player).Coordinates;

        // If we really want to spawn these entities, but we can't,
        // we remove everything that's in our way.
        if (spawnTiles.Count == 0)
        {
            var tileRef = _map.GetTileRef(rule.WorldMap, grid, playerCoords);
            var box = Box2.CenteredAround(Turf.GetTileCenter(tileRef).Position, Vector2.One);

            foreach (var entity in _lookup.GetEntitiesIntersecting(rule.WorldMap, box, LookupFlags.Static))
            {
                Del(entity);
            }

            spawnTiles.Add(tileRef);
        }

        // Spawn player equipment
        if (_equipment.GetPlayerEquipment(session.UserId) is { } equipment)
        {
            foreach (var (protoId, count) in equipment)
            {
                for (var i = 0; i < count; i++)
                {
                    var tileCenter = Turf.GetTileCenter(_random.Pick(spawnTiles));
                    var randomOffset = new Vector2(_random.NextFloat(-0.35f, 0.35f), _random.NextFloat(-0.35f, 0.35f));
                    var equip = Spawn(protoId, new EntityCoordinates(tileCenter.EntityId, tileCenter.Position + randomOffset));

                    _ownership.AddOwnership(equip, owner: player);
                }
            }
        }

        // Spawn roundstart pops
        foreach (var (_, profile) in prefs.Characters.Take(_cvar.GetCVar(RfVars.MaxRoundstartPops)))
        {
            var coords = Turf.GetTileCenter(_random.Pick(spawnTiles));
            var job = PickPopJob(profile.JobPriorities) ?? rule.DefaultPopsJob;
            var pop = _station.SpawnPlayerMob(coords, job, profile, null);

            if (_prototype.TryIndex(rule.PopsComponentsOverride, out var overrides))
                EntityManager.AddComponents(pop, overrides.Components);

            pops.Add(pop);
        }

        AddPops(player, pops);

        player.Comp.GotRoundstartPops = true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_debugSubscribers.Count == 0)
            return;

        var coords = AllPlayersSettlements()
            .Select(x
                => (GetNetEntity(x.Key), x.Value.Select(y => GetNetCoordinates(y)).ToList()))
            .ToDictionary();
        var msg = new SettlementCoordinatesMessage(coords);

        foreach (var subscriber in _debugSubscribers)
        {
            RaiseNetworkEvent(msg, subscriber);
        }
    }
}
