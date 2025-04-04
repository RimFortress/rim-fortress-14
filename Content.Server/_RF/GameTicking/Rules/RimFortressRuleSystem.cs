using System.Linq;
using System.Numerics;
using Content.Server._RF.World;
using Content.Server.GameTicking.Rules;
using Content.Shared._RF.GameTicking.Rules;
using Content.Shared._RF.World;
using Content.Shared.EntityTable;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._RF.GameTicking.Rules;

/// <summary>
/// Manages <see cref="RimFortressRuleComponent"/>
/// </summary>
public sealed class RimFortressRuleSystem : GameRuleSystem<RimFortressRuleComponent>
{
    [Dependency] private readonly RimFortressWorldSystem _world = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly EntityTableSystem _table = default!;

    private readonly Dictionary<EntityUid, Dictionary<EntProtoId, TimeSpan>> _nextEventTime = new ();
    private readonly List<Entity<WorldMapComponent>> _eventQueue = new();

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerBeforeSpawnEvent>(OnBeforeSpawn);
        SubscribeLocalEvent<WorldMapAvailableForEvent>(OnMapAvailable);
    }

    protected override void Added(EntityUid uid, RimFortressRuleComponent comp, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        base.Added(uid, comp, gameRule, args);

        _world.InitializeWorld(comp);
    }

    private void OnBeforeSpawn(PlayerBeforeSpawnEvent ev)
    {
        var query = EntityQueryEnumerator<RimFortressRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out _, out var rule))
        {
            if (!GameTicker.IsGameRuleActive(uid, rule))
                continue;

            _world.CreateOwnerMap(ev.Player);

            ev.Handled = true;
            return;
        }
    }

    private void OnMapAvailable(WorldMapAvailableForEvent ev)
    {
        var query = EntityQueryEnumerator<RimFortressRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var ruleUid, out var rf, out var rule))
        {
            if (!GameTicker.IsGameRuleActive(ruleUid, rule))
                continue;

            if (ev.Map.Comp.OwnerPlayer is { } uid
                && TryComp(uid, out RimFortressPlayerComponent? player)
                && !player.GotRoundstartPops)
            {
                foreach (var map in player.OwnedMaps)
                {
                    var area = Box2.CenteredAround(Transform(uid).Coordinates.Position, new Vector2(rf.RoundStartSpawnRadius));
                    var pops = _world.SpawnPop(map, area, amount: rf.RoundstartPops, hardSpawn: true);
                    _world.AddPops((uid, player), pops);
                }

                player.GotRoundstartPops = true;
                return;
            }

            var addRule = _random.Pick(AvailableRules(ev.Map));
            ResetTime(rf, ev.Map, addRule);
            _eventQueue.Add(ev.Map);

            GameTicker.AddGameRule(addRule);
        }
    }

    private List<EntProtoId> AvailableRules(Entity<WorldMapComponent> uid)
    {
        var available = new List<EntProtoId>();

        var query = EntityQueryEnumerator<RimFortressRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var ruleUid, out var rf, out var rule))
        {
            if (!GameTicker.IsGameRuleActive(ruleUid, rule))
                continue;

            if (!_nextEventTime.TryGetValue(uid, out var times))
                return _table.GetSpawns(rf.WorldEvents).ToList();

            foreach (var spawn in _table.GetSpawns(rf.WorldEvents))
            {
                if (times.TryGetValue(spawn, out var time)
                    && time > _timing.CurTime)
                    continue;

                available.Add(spawn);
            }
        }

        return available;
    }

    private void ResetTime(RimFortressRuleComponent component, EntityUid uid, EntProtoId eventId)
    {
        if (!_nextEventTime.ContainsKey(uid))
            _nextEventTime[uid] = new();

        _nextEventTime[uid][eventId] = _timing.CurTime + TimeSpan.FromSeconds(component.MinMaxEventTiming.Next(_random));
    }

    public Entity<WorldMapComponent>? GetWorldMap()
    {
        return _eventQueue.Pop();
    }
}
