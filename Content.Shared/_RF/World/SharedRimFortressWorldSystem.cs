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
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] protected readonly TurfSystem Turf = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IConfigurationManager _cvar = default!;
    [Dependency] private readonly SharedBiomeSystem _biome = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedGameTicker _ticker = default!;

    protected RimFortressRuleComponent? Rule;

    protected const byte ChunkSize = SharedBiomeSystem.ChunkSize;

    protected EntityQuery<RimFortressPlayerComponent> PlayerQuery;
    private EntityQuery<TransformComponent> _xformQuery;

    private int _maxSettlementRadius = 100;
    private int _minSettlementMembers = 2;
    private int _playerSafeRadius = 100;
    protected int SpawnAreaRadius = 20;
    protected int MinSpawnAreaTiles = 100;

    public override void Initialize()
    {
        base.Initialize();
        PlayerQuery = GetEntityQuery<RimFortressPlayerComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();

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

    public TimeSpan WorldDateTime()
    {
        var query = EntityQueryEnumerator<RimFortressRuleComponent>();
        while (query.MoveNext(out var comp))
        {
            if (!TryComp(comp.WorldMap, out LightCycleComponent? cycle))
                return TimeSpan.Zero;

            var time = _timing.CurTime
                .Add(cycle.Offset)
                .Subtract(_ticker.RoundStartTimeSpan);

            // We take the length of an in-game day in 24 hours and get the current time
            var gameTimeHours = time.TotalSeconds % cycle.Duration.TotalSeconds
                / cycle.Duration.TotalSeconds * 24;

            var hours = (int) gameTimeHours;
            var minutes = (int) ((gameTimeHours - hours) * 60);

            var days = (int) Math.Floor(time / cycle.Duration) + 1;

            return TimeSpan.FromDays(days, hours, minutes);
        }

        return TimeSpan.Zero;
    }
}

[Serializable, NetSerializable]
public sealed class SettlementCoordinatesMessage(Dictionary<NetEntity, List<NetCoordinates>> coords) : EntityEventArgs
{
    public Dictionary<NetEntity, List<NetCoordinates>> Coords = coords;
}

[Serializable, NetSerializable]
public sealed class WorldDebugInfoRequest : EntityEventArgs
{
}
