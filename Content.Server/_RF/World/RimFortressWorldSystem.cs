using System.Linq;
using System.Numerics;
using Content.Server._RF.Equipment;
using Content.Server._RF.NPC;
using Content.Server.Administration.Managers;
using Content.Server.Mind;
using Content.Server.Parallax;
using Content.Server.Preferences.Managers;
using Content.Server.Station.Systems;
using Content.Shared._RF.GameTicking.Rules;
using Content.Shared._RF.World;
using Content.Shared._RF.CCVar;
using Content.Shared._RF.NPC;
using Content.Shared._RF.Parallax.Fog;
using Content.Shared.Administration;
using Content.Shared.Light.Components;
using Content.Shared.Pinpointer;
using Content.Shared.Preferences;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Configuration;
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
    [Dependency] private readonly IAdminManager _admin = default!;
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
    [Dependency] private readonly IConfigurationManager _cvar = default!;
    [Dependency] private readonly IPlayerEquipmentManager _equipment = default!;
    [Dependency] private readonly NpcControlSystem _npc = default!;

    private readonly HashSet<ICommonSession> _debugSubscribers = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<WorldDebugInfoRequest>(OnDebugRequest);
    }

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

    public EntityUid InitializeWorld(RimFortressRuleComponent rule)
    {
        Rule = rule;
        var map = _map.CreateMap();
        _biome.EnsurePlanet(map, _prototype.Index(rule.Biome));
        var fog = EnsureComp<FogOfWarComponent>(map);
        Dirty(map, fog);

        if (TryComp(map, out LightCycleComponent? cycle))
        {
            cycle.Duration = rule.DayDuration;
            cycle.Offset = rule.DayDuration / 3; // For roundstart day time
            cycle.InitialOffset = false;
            cycle.MinLightLevel = 1f;
        }

        WorldMap = map;
        return map;
    }

    /// <summary>
    /// Creates or allocates a free map for the player
    /// </summary>
    public void SpawnPlayer(ICommonSession session)
    {
        if (Rule is not { } rule || WorldMap is not { } worldMap)
            return ;

        var coords = Turf.GetTileCenter(GetSpawnTiles(1).First());
        var spawnBox = Box2.CenteredAround(coords.Position, new Vector2(SpawnAreaRadius));
        var freeTiles = GetFreeTiles(worldMap, spawnBox, MinSpawnAreaTiles);

        if (freeTiles.Count == 0)
            return;

        // Spawn RF player entity
        var newMind = _mind.CreateMind(session.UserId, session.Name);
        _mind.SetUserId(newMind, session.UserId);

        var mob = Spawn(rule.PlayerProtoId, coords);
        _mind.TransferTo(newMind, mob);

        var player = EnsureComp<RimFortressPlayerComponent>(mob);
        player.NextEventTime = _timing.CurTime + rule.MinimumTimeUntilFirstEvent;
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
            _npc.AddNpcControl(player.Owner, pop);

            var beacon = EnsureComp<NavMapBeaconComponent>(pop);
            beacon.Color = player.Comp.FactionColor;
            beacon.Text = MetaData(pop).EntityName;
        }

        player.Comp.Pops.AddRange(pops);
        Dirty(player);
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
            || WorldMap is not { } worldMap
            || !Resolve(player.Owner, ref player.Comp)
            || player.Comp.GotRoundstartPops
            || !_player.TryGetSessionByEntity(player, out var session))
            return;

        var pops = new List<EntityUid>();
        var prefs = _preferences.GetPreferences(session.UserId);
        var grid = Comp<MapGridComponent>(worldMap);
        var playerCoords = Transform(player).Coordinates;

        // If we really want to spawn these entities, but we can't,
        // we remove everything that's in our way.
        if (spawnTiles.Count == 0)
        {
            var tileRef = _map.GetTileRef(worldMap, grid, playerCoords);
            var box = Box2.CenteredAround(Turf.GetTileCenter(tileRef).Position, Vector2.One);

            foreach (var entity in _lookup.GetEntitiesIntersecting(worldMap, box, LookupFlags.Static))
            {
                EntityManager.DeleteEntity(entity);
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

                    EnsureComp<OwnedComponent>(equip).Owners.Add(player);
                }
            }
        }

        // Spawn roundstart pops
        foreach (var (_, profile) in prefs.Characters.Take(_cvar.GetCVar(RfVars.MaxRoundstartPops)))
        {
            var coords = Turf.GetTileCenter(_random.Pick(spawnTiles));
            var character = (HumanoidCharacterProfile) profile;
            var job = PickPopJob(character.JobPriorities) ?? rule.DefaultPopsJob;
            var pop = _station.SpawnPlayerMob(coords, job, character, null);

            if (rule.PopsComponentsOverride != null)
                EntityManager.AddComponents(pop, rule.PopsComponentsOverride, false);

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
