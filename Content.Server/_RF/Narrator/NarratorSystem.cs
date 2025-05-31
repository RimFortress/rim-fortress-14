using Content.Server._RF.GameTicking.Rules;
using Content.Server._RF.World;
using Content.Server.Cargo.Systems;
using Content.Server.Construction.Components;
using Content.Server.GameTicking;
using Content.Shared._RF.CCVar;
using Content.Shared._RF.GameTicking.Rules;
using Content.Shared._RF.Narrator;
using Content.Shared._RF.NPC;
using Content.Shared._RF.World;
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
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly PricingSystem _pricing = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly RimFortressRuleSystem _rule = default!;
    [Dependency] private readonly RimFortressWorldSystem _world = default!;
    [Dependency] private readonly IConsoleHost _host  = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    private EntityQuery<ItemComponent> _itemQuery;
    private EntityQuery<ConstructionComponent> _constructionQuery;

    private readonly Dictionary<EntityUid, int> _lastWaitPoint = new();

    public override void Initialize()
    {
        base.Initialize();

        _itemQuery = GetEntityQuery<ItemComponent>();
        _constructionQuery = GetEntityQuery<ConstructionComponent>();
        _prototype.PrototypesReloaded += args =>
        {
            if (args.WasModified<NarratorPrototype>())
                InitPrototypes();
        };

        InitPrototypes();
        InitializeCommands();
    }

    private void InitPrototypes()
    {
        foreach (var proto in _prototype.EnumeratePrototypes<NarratorPrototype>())
        {
            foreach (var curve in proto.MoodCurves)
            {
                curve.Initialize();
            }

            foreach (var curve in proto.EventChanceCurves)
            {
                curve.Initialize();
            }

            foreach (var curve in proto.WealthCurves)
            {
                curve.Initialize();
            }
        }
    }

    /// <summary>
    /// Counts the number of wealth points of the player's settlement
    /// </summary>
    public float SettlementWealth(Entity<RimFortressPlayerComponent?> player, ProtoId<NarratorPrototype> proto, EntityCoordinates settlement)
    {
        if (!Resolve(player, ref player.Comp))
            return 0;

        double itemCost = 0;
        double constructionCost = 0;
        double popCost = 0;

        var settlementRadius = _cfg.GetCVar(RfVars.MaxSettlementRadius);
        var narrator = _prototype.Index(proto);
        var entities = EntityQueryEnumerator<OwnedComponent, TransformComponent>();

        while (entities.MoveNext(out var uid, out var comp, out var xform))
        {
            if (!comp.Owners.Contains(player)
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

        return (float) (itemCost + constructionCost * narrator.ConstructionCostMod + popCost);
    }

    /// <summary>
    /// Counts the narrator's event points that can be spent on summoning events
    /// </summary>
    public int EventPoints(Entity<RimFortressPlayerComponent?> player,
        ProtoId<NarratorPrototype> proto,
        EntityCoordinates settlement)
    {
        if (!Resolve(player, ref player.Comp))
            return 0;

        var narrator = _prototype.Index(proto);
        var waitPoints = WaitPoint(narrator, player.Comp);
        var wealth = SettlementWealth(player, proto, settlement);
        var narratorMood = 0f;

        foreach (var curve in narrator.WealthCurves)
        {
            wealth = curve.Curve(wealth);
        }

        foreach (var curve in narrator.MoodCurves)
        {
            narratorMood = curve.Curve(narratorMood);
        }

        return (int) Math.Floor(wealth + waitPoints * narratorMood);
    }

    public int GlobalEventPoints(RimFortressRuleComponent rule)
    {
        var narrator = _prototype.Index(rule.Narrator);
        var waitPoints = GlobalWaitPoint(rule);
        var narratorMood = 0f;

        foreach (var curve in narrator.MoodCurves)
        {
            narratorMood = curve.Curve(narratorMood);
        }

        return (int) Math.Floor(waitPoints * narratorMood);
    }

    /// <summary>
    /// Returns the points that have accumulated during the time without events
    /// </summary>
    public int WaitPoint(NarratorPrototype narrator, RimFortressPlayerComponent player)
    {
        return (int) Math.Floor((_ticker.RoundDuration() - player.LastEventTime).TotalSeconds * narrator.EventWaitFactor);
    }

    public int GlobalWaitPoint(RimFortressRuleComponent rule)
    {
        var narrator = _prototype.Index(rule.Narrator);
        return (int) Math.Floor((_ticker.RoundDuration() - rule.LastEventTime).TotalSeconds * narrator.EventWaitFactor);
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
                    float chance = newPoints;

                    foreach (var curve in proto.EventChanceCurves)
                    {
                        chance = curve.Curve(chance);
                    }

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
                float globalChance = globalWaitPoints;

                foreach (var curve in proto.EventChanceCurves)
                {
                    globalChance = curve.Curve(globalChance);
                }

                if (_random.NextFloat() < globalChance)
                {
                    var available = new List<EntProtoId>();

                    foreach (var (eventId, points) in comp.GlobalEvents)
                    {
                        if (points > globalPoints)
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

    private string DebugText(Entity<RimFortressPlayerComponent?> player, ProtoId<NarratorPrototype> protoId)
    {
        if (!Resolve(player, ref player.Comp) || !_prototype.TryIndex(protoId, out var proto))
            return "Unknown";

        var text = "";
        var waitPoints = WaitPoint(proto, player.Comp);
        var narratorMood = 0f;
        var rules = _rule.AvailableRules(new(player, player.Comp));
        float chance = waitPoints;

        foreach (var curve in proto.MoodCurves)
        {
            narratorMood = curve.Curve(narratorMood);
        }

        foreach (var curve in proto.EventChanceCurves)
        {
            chance = curve.Curve(chance);
        }

        foreach (var coords in _world.GetPlayerSettlements(player))
        {
            var wealth = SettlementWealth(player, proto, coords);
            var wealthFactor = wealth;

            foreach (var curve in proto.WealthCurves)
            {
                wealthFactor = curve.Curve(wealthFactor);
            }


            var events = "";
            foreach (var (_, eventId, comp) in rules)
            {
                var points = EventPoints(player, protoId, coords);

                if (comp.Cost > points)
                    continue;

                events += $"- {eventId}";
            }

            text += "=================================\n" +
                    $"Settlement: {coords}\n" +
                    $"Settlement wealth: {wealth}\n" +
                    $"Event wait points: {waitPoints}\n" +
                    $"Round time seconds: {_ticker.RoundDuration().TotalSeconds}\n" +
                    $"Narrator mood: {narratorMood}\n" +
                    $"Event points (without random factor): {wealthFactor + waitPoints * narratorMood}\n" +
                    $"Current event chance(0-1): {chance}\n" +
                    $"Available events: \n{events}\n";
        }

        return text;
    }
}
