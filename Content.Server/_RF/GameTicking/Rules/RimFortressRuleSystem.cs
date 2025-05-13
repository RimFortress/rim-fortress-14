using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server._RF.World;
using Content.Server.GameTicking.Rules;
using Content.Shared._RF.GameTicking.Rules;
using Content.Shared._RF.World;
using Content.Shared.EntityTable;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
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
    [Dependency] private readonly TransformSystem _transform = default!;

    private readonly Dictionary<EntityUid, Dictionary<EntProtoId, TimeSpan>> _nextEventTime = new ();
    private readonly List<Entity<RimFortressPlayerComponent>> _eventQueue = new();

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerBeforeSpawnEvent>(OnBeforeSpawn);
        SubscribeLocalEvent<PlayerAvailableForEvent>(OnPlayerAvailable);
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

            _world.AddSpawnQueue(ev.Player);

            ev.Handled = true;
            return;
        }
    }

    private void OnPlayerAvailable(PlayerAvailableForEvent ev)
    {
        var query = EntityQueryEnumerator<RimFortressRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var ruleUid, out var rf, out var rule))
        {
            if (!GameTicker.IsGameRuleActive(ruleUid, rule))
                continue;

            var addRule = _random.Pick(AvailableRules(ev.Player));
            ResetTime(rf, ev.Player, addRule);
            _eventQueue.Add(ev.Player);

            GameTicker.AddGameRule(addRule);
            GameTicker.StartGameRule(addRule);
        }
    }

    private List<EntProtoId> AvailableRules(Entity<RimFortressPlayerComponent> uid)
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

    public bool TryGetEvent(
        [NotNullWhen(true)] out EntityUid? gridUid,
        [NotNullWhen(true)] out EntityCoordinates? coords,
        [NotNullWhen(true)] out Entity<RimFortressPlayerComponent>? player)
    {
        coords = null;
        player = null;
        gridUid = null;

        if (_eventQueue.Count == 0)
            return false;

        player = _eventQueue.Pop();
        var settlements = _world.GetPlayerSettlements(player.Value.Owner);

        if (settlements.Count == 0)
            return false;

        coords = _random.Pick(settlements);
        gridUid = _transform.GetGrid(coords.Value);
        return gridUid != null;
    }
}
