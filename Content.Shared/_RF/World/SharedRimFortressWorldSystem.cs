using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared._RF.CCVar;
using Content.Shared._RF.GameTicking.Rules;
using Content.Shared.GameTicking;
using Content.Shared.Light.Components;
using Content.Shared.Maps;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Pinpointer;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared._RF.World;

public abstract partial class SharedRimFortressWorldSystem : EntitySystem
{
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] protected TurfSystem Turf = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IConfigurationManager _cvar = default!;
    [Dependency] private SharedBiomeSystem _biome = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedGameTicker _ticker = default!;

    protected RimFortressRuleComponent? Rule;

    protected const byte ChunkSize = SharedBiomeSystem.ChunkSize;

    [Dependency] protected EntityQuery<RimFortressPlayerComponent> PlayerQuery;
    [Dependency] private EntityQuery<TransformComponent> _xformQuery;

    private int _maxSettlementRadius = 100;
    private int _minSettlementMembers = 2;
    private int _playerSafeRadius = 100;
    protected int SpawnAreaRadius = 20;
    protected int MinSpawnAreaTiles = 100;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cvar, RfVars.MaxSettlementRadius, value => _maxSettlementRadius = value, true);
        Subs.CVar(_cvar, RfVars.MinSettlementMembers, value => _minSettlementMembers = value, true);
        Subs.CVar(_cvar, RfVars.PlayerSafeRadius, value => _playerSafeRadius = value, true);
        Subs.CVar(_cvar, RfVars.SpawnAreaRadius, value => SpawnAreaRadius = value, true);
        Subs.CVar(_cvar, RfVars.MinSpawnAreaTiles, value => MinSpawnAreaTiles = value, true);
    }

    public List<EntityUid>? GetPLayerPops(EntityUid uid)
    {
        if (!PlayerQuery.TryComp(uid, out var player))
            return null;

        return player.Pops.Count == 0 ? null : player.Pops;
    }

    public void CreateMapBeacon(EntityUid gridUid, Vector2i indicates, Color color, string text)
    {
        var coords = _map.ToCoordinates(gridUid, indicates);
        var uid = Spawn(null, coords);

        var comp = EnsureComp<NavMapBeaconComponent>(uid);
        comp.Color = color;
        comp.Text = text;
    }

    public void ChangeBeacon(Entity<NavMapBeaconComponent?> entity, Color color, string text)
    {
        if (!Resolve(entity, ref entity.Comp))
            return;

        entity.Comp.Color = color;
        entity.Comp.Text = text;
    }

    public void SetPlayerFactionColor(Entity<RimFortressPlayerComponent?> uid, Color color)
    {
        if (!Resolve(uid, ref uid.Comp))
            return;

        uid.Comp.FactionColor = color;

        if (_net.IsServer)
            Dirty(uid);

        foreach (var pop in uid.Comp.Pops)
        {
            if (!TryComp(pop, out NavMapBeaconComponent? beacon))
                continue;

            beacon.Color = color;

            if (_net.IsServer)
                Dirty(pop, beacon);
        }
    }

    public ProtoId<JobPrototype>? PickPopJob(IReadOnlyDictionary<ProtoId<JobPrototype>, JobPriority> jobPriorities)
    {
        if (TryPick(JobPriority.High, out var picked))
            return picked;

        if (TryPick(JobPriority.Medium, out picked))
            return picked;

        if (TryPick(JobPriority.Low, out picked))
            return picked;

        return null;

        bool TryPick(JobPriority priority, [NotNullWhen(true)] out ProtoId<JobPrototype>? jobId)
        {
            var filtered = jobPriorities
                .Where(p => p.Value == priority)
                .Select(p => p.Key)
                .ToList();

            if (filtered.Count != 0)
            {
                jobId = _random.Pick(filtered);
                return true;
            }

            jobId = null;
            return false;
        }
    }

    /// <summary>
    /// Returns current world time
    /// </summary>
    public TimeSpan WorldDateTime()
    {
        if (TryGetWorld(out var uid) && TryComp(uid, out LightCycleComponent? cycle))
        {
            return ToWorldTime(new(uid.Value, cycle),
                _timing.CurTime.Add(cycle.Offset).Subtract(_ticker.RoundStartTimeSpan));
        }

        return TimeSpan.Zero;
    }

    /// <summary>
    /// Converts the in-game simulation time to world time
    /// </summary>
    public TimeSpan ToWorldTime(TimeSpan time)
        => TryGetWorld(out var uid) ? ToWorldTime(uid.Value, time) : time;

    /// <summary>
    /// Converts the in-game simulation time to world time
    /// </summary>
    public TimeSpan ToWorldTime(Entity<LightCycleComponent?> ent, TimeSpan time)
    {
        if (!Resolve(ent, ref ent.Comp))
            return time;

        if (time < TimeSpan.Zero)
            return TimeSpan.Zero;

        // Calculate days (starting from 1)
        var totalDays = time.TotalSeconds / ent.Comp.Duration.TotalSeconds;
        var days = (int)Math.Floor(totalDays);

        // Calculate time within current day (0.0 to 1.0)
        var dayFraction = totalDays - Math.Floor(totalDays);

        // Combine days and time of day
        return TimeSpan.FromDays(days) + TimeSpan.FromHours(dayFraction * 24);
    }

    /// <summary>
    /// Converts the world time to in-game simulation time
    /// </summary>
    public TimeSpan? FromWorldTime(TimeSpan? worldTime)
        => worldTime != null ? FromWorldTime(worldTime.Value) : null;

    /// <summary>
    /// Converts the world time to in-game simulation time
    /// </summary>
    public TimeSpan FromWorldTime(TimeSpan worldTime)
        => TryGetWorld(out var uid) ? FromWorldTime(uid.Value, worldTime) : worldTime;

    /// <summary>
    /// Converts the world time to in-game simulation time
    /// </summary>
    public TimeSpan FromWorldTime(Entity<LightCycleComponent?> ent, TimeSpan worldTime)
    {
        if (!Resolve(ent, ref ent.Comp))
            return worldTime;

        if (worldTime <= TimeSpan.Zero)
            return _ticker.RoundStartTimeSpan - ent.Comp.Offset;

        var timeOfDay = worldTime - TimeSpan.FromDays(worldTime.Days);
        var dayFraction = timeOfDay.TotalHours / 24.0;
        var totalGameDays = worldTime.Days + dayFraction;

        return TimeSpan.FromTicks((long)(totalGameDays * ent.Comp.Duration.Ticks));
    }

    /// <summary>
    /// Returns the world map entity
    /// </summary>
    public bool TryGetWorld([NotNullWhen(true)] out EntityUid? ent)
    {
        var query = AllEntityQuery<RimFortressRuleComponent>();

        while (query.MoveNext(out var comp))
        {
            if (!Exists(comp.WorldMap))
                continue;

            ent = comp.WorldMap;
            return true;
        }

        ent = null;
        return false;
    }
}

[Serializable, NetSerializable]
public sealed class SettlementCoordinatesMessage(Dictionary<NetEntity, List<NetCoordinates>> coords) : EntityEventArgs
{
    public Dictionary<NetEntity, List<NetCoordinates>> Coords = coords;
}

[Serializable, NetSerializable]
public sealed class WorldDebugInfoRequest : EntityEventArgs;
