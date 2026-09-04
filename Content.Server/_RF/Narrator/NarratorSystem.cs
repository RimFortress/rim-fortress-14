using Content.Server._RF.GameTicking.Rules;
using Content.Server._RF.World;
using Content.Server.Cargo.Systems;
using Content.Server.Construction.Components;
using Content.Server.GameTicking;
using Content.Shared._RF.CCVar;
using Content.Shared._RF.GameTicking.Rules;
using Content.Shared._RF.MathHelpers.MathCurve.Systems;
using Content.Shared._RF.Narrator;
using Content.Shared._RF.NPC.Components;
using Content.Shared._RF.NPC.Systems;
using Content.Shared._RF.World.Components;
using Content.Shared.EntityTable;
using Content.Shared.Item;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._RF.Narrator;

public sealed partial class NarratorSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private PricingSystem _pricing = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private RimFortressRuleSystem _rule = default!;
    [Dependency] private RimFortressWorldSystem _world = default!;
    [Dependency] private IConsoleHost _host = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private EntityTableSystem _table = default!;
    [Dependency] private OwnershipSystem _ownership = default!;
    [Dependency] private MathCurvesSystem _curves = default!;

    [Dependency] private EntityQuery<ItemComponent> _itemQuery;
    [Dependency] private EntityQuery<ConstructionComponent> _constructionQuery;

    private readonly Dictionary<EntityUid, int> _lastWaitPoint = new();

    public override void Initialize()
    {
        base.Initialize();
        InitializeCommands();
    }

    /// <summary>
    /// Counts the number of wealth points of the player's settlement
    /// </summary>
    public float SettlementWealth(Entity<RimFortressPlayerComponent?> player, ProtoId<NarratorPrototype> proto, EntityCoordinates settlement)
    {
        if (!Resolve(player, ref player.Comp)
            || !_prototype.TryIndex(proto, out var narrator))
            return 0;

        double itemCost = 0;
        double constructionCost = 0;
        double popCost = 0;

        var settlementRadius = _cfg.GetCVar(RfVars.MaxSettlementRadius);
        var entities = EntityQueryEnumerator<OwnershipComponent, TransformComponent>();

        while (entities.MoveNext(out var uid, out var comp, out var xform))
        {
            if (!_ownership.HasOwner(new(uid, comp), player)
                || !settlement.TryDistance(EntityManager, xform.Coordinates, out var dist)
                || dist > settlementRadius)
                continue;

            if (_itemQuery.TryComp(uid, out _))
                itemCost += _pricing.GetPrice(uid);
            else if (_constructionQuery.TryComp(uid, out _))
                constructionCost += _pricing.GetPrice(uid);
        }

        foreach (var pop in player.Comp.Pops)
        {
            if (!TryComp(pop, out TransformComponent? xform)
                || !settlement.TryDistance(EntityManager, xform.Coordinates, out var dist)
                || dist > settlementRadius)
                continue;

            popCost += _pricing.GetPrice(pop);
        }

        return (float)(itemCost + constructionCost * narrator.ConstructionCostMod + popCost);
    }

    /// <summary>
    /// Counts the narrator's event points that can be spent on summoning events
    /// </summary>
    public int EventPoints(Entity<RimFortressPlayerComponent?> player,
        ProtoId<NarratorPrototype> proto,
        EntityCoordinates settlement)
    {
        if (!Resolve(player, ref player.Comp)
            || !_prototype.TryIndex(proto, out var narrator))
            return 0;

        var waitPoints = WaitPoint(narrator, player.Comp);
        var wealth = _curves.Get(narrator.WealthCurves, SettlementWealth(player, proto, settlement));
        var narratorMood = _curves.Get(narrator.MoodCurves);

        return (int)Math.Floor(wealth + waitPoints * narratorMood);
    }

    public int GlobalEventPoints(RimFortressRuleComponent rule)
    {
        if (!_prototype.TryIndex(rule.Narrator, out var narrator))
            return 0;

        var waitPoints = GlobalWaitPoint(rule);
        var narratorMood = _curves.Get(narrator.MoodCurves);

        return (int)Math.Floor(waitPoints * narratorMood);
    }

    /// <summary>
    /// Returns the points that have accumulated during the time without events
    /// </summary>
    public int WaitPoint(NarratorPrototype narrator, RimFortressPlayerComponent player)
    {
        return (int)Math.Floor((_ticker.RoundDuration() - player.LastEventTime).TotalSeconds * narrator.EventWaitFactor);
    }

    public int GlobalWaitPoint(RimFortressRuleComponent rule)
    {
        if (!_prototype.TryIndex(rule.Narrator, out var narrator))
            return 0;

        return (int)Math.Floor((_ticker.RoundDuration() - rule.LastEventTime).TotalSeconds * narrator.EventWaitFactor);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var enumerator = EntityQueryEnumerator<RimFortressRuleComponent>();
        while (enumerator.MoveNext(out var comp))
        {
            if (!_prototype.TryIndex(comp.Narrator, out var proto))
                continue;

            // Local player events
            var entities = EntityQueryEnumerator<RimFortressPlayerComponent>();
            while (entities.MoveNext(out var uid, out var player))
            {
                var newPoints = WaitPoint(proto, player);

                if (_lastWaitPoint.TryGetValue(uid, out var point) && point != newPoints)
                {
                    var chance = _curves.Get(proto.EventChanceCurves, newPoints);

                    if (_random.NextFloat() < chance
                        && PickRandomEvent(new(uid, player), comp.Narrator) is { } ev)
                    {
                        _rule.StartWorldRule(ev.Proto, uid, ev.Coords);

                        player.LastEventTime = _ticker.RoundDuration();
                        _lastWaitPoint[uid] = 0;
                        continue;
                    }
                }

                _lastWaitPoint[uid] = newPoints;
            }

            if (comp.GlobalEvents == null)
                continue;

            // Global world events
            var globalWaitPoints = GlobalWaitPoint(comp);
            var globalPoints = GlobalEventPoints(comp);

            if (comp.LastWaitPoints != globalWaitPoints)
            {
                var globalChance = _curves.Get(proto.EventChanceCurves, globalWaitPoints);
                var chance = _random.NextFloat();
                var available = new List<EntProtoId>();

                foreach (var eventId in _table.GetSpawns(comp.GlobalEvents))
                {
                    if (!_prototype.TryIndex(eventId, out var ent)
                        || !ent.TryComp(out GlobalWorldRuleComponent? rule, EntityManager.ComponentFactory)
                        || chance > globalChance * rule.ChanceMod
                        || rule.Cost > globalPoints)
                        continue;

                    available.Add(eventId);
                }

                if (available.Count > 0)
                {
                    _ticker.StartGameRule(_random.Pick(available));
                    comp.LastEventTime = _ticker.RoundDuration();
                    comp.LastWaitPoints = 0;
                }
            }

            comp.LastWaitPoints = globalWaitPoints;
        }
    }

    /// <summary>
    /// Returns a random event that the narrator can trigger
    /// </summary>
    public (EntityCoordinates Coords, EntProtoId Proto)? PickRandomEvent(
        Entity<RimFortressPlayerComponent?> player,
        ProtoId<NarratorPrototype> narrator)
    {
        if (!Resolve(player, ref player.Comp))
            return null;

        var available = new List<(EntityCoordinates Coords, EntProtoId Proto)>();
        var rules = _rule.AvailableRules(new(player, player.Comp));

        foreach (var (coords, proto, comp) in rules)
        {
            var points = EventPoints(player, narrator, coords);

            if (comp.Cost > points)
                continue;

            available.Add((coords, proto));
        }

        if (available.Count == 0)
            return null;

        return _random.Pick(available);
    }

    private string DebugTextGlobal()
    {
        if (_rule.GetRule() is not { } rule)
            return string.Empty;

        if (!_prototype.TryIndex(rule.Narrator, out var narrator))
            return "Unknown";

        var waitPoints = GlobalEventPoints(rule);
        var narratorMood = _curves.Get(narrator.MoodCurves);
        var chance = _curves.Get(narrator.EventChanceCurves, waitPoints);
        var events = "";

        foreach (var eventId in _table.GetSpawns(rule.GlobalEvents))
        {
            if (!_prototype.TryIndex(eventId, out var proto)
                || !proto.TryComp(out GlobalWorldRuleComponent? globalRule, EntityManager.ComponentFactory)
                || globalRule.Cost > waitPoints * narratorMood)
                continue;

            events += $"- {eventId}: {(int)Math.Floor(chance * globalRule.ChanceMod * 100)}%\n";
        }

        return "========GLOBAL EVENTS INFO========\n" +
                $"Event wait points: {waitPoints}\n" +
                $"Round time seconds: {_ticker.RoundDuration().TotalSeconds}\n" +
                $"Narrator mood: {narratorMood}\n" +
                $"Event points: {waitPoints * narratorMood}\n" +
                $"Current event chance(0-1): {chance}\n" +
                $"Available events: \n{events}";
    }

    private string DebugText(Entity<RimFortressPlayerComponent?> player, ProtoId<NarratorPrototype> protoId)
    {
        if (!Resolve(player, ref player.Comp) || !_prototype.TryIndex(protoId, out var proto))
            return "Unknown";

        var text = "";
        var waitPoints = WaitPoint(proto, player.Comp);
        var narratorMood = _curves.Get(proto.MoodCurves);
        var rules = _rule.AvailableRules(new(player, player.Comp));
        var chance = _curves.Get(proto.EventChanceCurves, waitPoints);

        foreach (var coords in _world.GetPlayerSettlements(player))
        {
            var wealth = SettlementWealth(player, proto, coords);
            var wealthFactor = _curves.Get(proto.WealthCurves, wealth);

            var events = "";
            foreach (var (_, eventId, comp) in rules)
            {
                var points = EventPoints(player, protoId, coords);

                if (comp.Cost > points)
                    continue;

                events += $"- {eventId}\n";
            }

            text += "=================================\n" +
                    $"Settlement: {coords}\n" +
                    $"Settlement wealth: {wealth}\n" +
                    $"Event wait points: {waitPoints}\n" +
                    $"Round time seconds: {_ticker.RoundDuration().TotalSeconds}\n" +
                    $"Narrator mood: {narratorMood}\n" +
                    $"Event points: {wealthFactor + waitPoints * narratorMood}\n" +
                    $"Current event chance(0-1): {chance}\n" +
                    $"Available events: \n{events}";
        }

        return text;
    }
}
