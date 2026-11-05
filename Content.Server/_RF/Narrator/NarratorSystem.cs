using Content.Server._RF.GameTicking.Rules;
using Content.Server._RF.World.Settlements;
using Content.Server.Administration.Logs;
using Content.Server.GameTicking;
using Content.Server.Parallax;
using Content.Shared._RF.GameTicking.Rules;
using Content.Shared.Database;
using Content.Shared._RF.MathHelpers.MathCurve.Systems;
using Content.Shared._RF.Narrator;
using Content.Shared._RF.NPC.Systems;
using Content.Shared._RF.Phases;
using Content.Shared._RF.World.Settlements;
using Content.Shared.EntityTable;
using Content.Shared.GameTicking.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._RF.Narrator;

/// <summary>
/// The narrator: it decides when events happen to the settlements, and which. The events depend on the settlements alone, each has
/// its own points (see <see cref="SettlementEventsComponent"/> for the wait for events and <see cref="SettlementWealthComponent"/> for
/// what the settlement is worth), and the narrator looks at them once in a while, see <see cref="NarratorRuleComponent.UpdateInterval"/>.
/// </summary>
public sealed partial class NarratorSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototype = null!;
    [Dependency] private IRobustRandom _random = null!;
    [Dependency] private IGameTiming _timing = null!;
    [Dependency] private GameTicker _ticker = null!;
    [Dependency] private RimFortressRuleSystem _rule = null!;
    [Dependency] private EntityTableSystem _table = null!;
    [Dependency] private MathCurvesSystem _curves = null!;
    [Dependency] private SettlementSystem _settlements = null!;
    [Dependency] private SettlementWealthSystem _wealth = null!;
    [Dependency] private IAdminLogManager _adminLogger = null!;
    [Dependency] private PhasesSystem _phases = null!;
    [Dependency] private OwnershipSystem _ownership = null!;
    [Dependency] private TransformSystem _transform = null!;
    [Dependency] private BiomeSystem _biome = null!;
    [Dependency] private MapSystem _map = null!;

    [Dependency] private EntityQuery<SettlementEventsComponent> _eventsQuery;
    [Dependency] private EntityQuery<SettlementWealthComponent> _wealthQuery;

    /// <summary>
    /// The wealth of the settlement for the narrator: the categories of it, each counted for as much as the narrator values it.
    /// </summary>
    public float SettlementWealth(Entity<SettlementComponent> settlement, ProtoId<NarratorPrototype> proto)
    {
        if (!_prototype.TryIndex(proto, out var narrator) || !_wealthQuery.TryComp(settlement, out var wealth))
            return 0;

        var total = 0f;

        foreach (var (category, value) in wealth.Values)
        {
            total += CategoryWealth(narrator, category, value);
        }

        return total;
    }

    /// <summary>
    /// What a category of the wealth counts for to the narrator.
    /// </summary>
    public float CategoryWealth(NarratorPrototype narrator, string category, double price)
        => narrator.WealthWeights.TryGetValue(category, out var curves) && curves.Count > 0
            ? _curves.Get(curves, (float) price)
            : (float) price;

    /// <summary>
    /// Counts the narrator's event points of a settlement that can be spent on summoning events.
    /// </summary>
    public int EventPoints(Entity<SettlementComponent> settlement, ProtoId<NarratorPrototype> proto)
    {
        if (!_prototype.TryIndex(proto, out var narrator) || !_eventsQuery.TryComp(settlement, out var events))
            return 0;

        var waitPoints = WaitPoint(narrator, events);
        var wealth = _curves.Get(narrator.WealthCurves, SettlementWealth(settlement, proto));
        var narratorMood = _curves.Get(narrator.MoodCurves);

        return (int) Math.Floor(wealth + waitPoints * narratorMood) + events.BonusPoints;
    }

    public int GlobalEventPoints(NarratorRuleComponent rule)
    {
        if (!_prototype.TryIndex(rule.Narrator, out var narrator))
            return 0;

        var waitPoints = GlobalWaitPoint(rule);
        var narratorMood = _curves.Get(narrator.MoodCurves);

        return (int) Math.Floor(waitPoints * narratorMood);
    }

    /// <summary>
    /// Returns the points that have accumulated during the time without events.
    /// </summary>
    public int WaitPoint(NarratorPrototype narrator, SettlementEventsComponent events)
    {
        return (int) Math.Floor((_ticker.RoundDuration() - events.LastEventTime).TotalSeconds * narrator.EventWaitFactor);
    }

    public int GlobalWaitPoint(NarratorRuleComponent rule)
    {
        if (!_prototype.TryIndex(rule.Narrator, out var narrator))
            return 0;

        return (int) Math.Floor((_ticker.RoundDuration() - rule.LastEventTime).TotalSeconds * narrator.EventWaitFactor);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var enumerator = EntityQueryEnumerator<NarratorRuleComponent, ActiveGameRuleComponent>();

        while (enumerator.MoveNext(out var uid, out var comp, out _))
        {
            if (now < comp.NextUpdate)
                continue;

            comp.NextUpdate = now + comp.UpdateInterval;

            if (!_prototype.TryIndex(comp.Narrator, out var proto))
                continue;

            UpdateSettlements((uid, comp), proto);
            UpdateGlobal((uid, comp), proto);
        }
    }

    // Local events of the settlements.
    private void UpdateSettlements(Entity<NarratorRuleComponent> rule, NarratorPrototype narrator)
    {
        foreach (var settlement in _settlements.AllSettlements())
        {
            if (!_eventsQuery.TryComp(settlement, out var events))
                continue;

            var newPoints = WaitPoint(narrator, events);

            if (events.LastWaitPoints == newPoints)
                continue;

            events.LastWaitPoints = newPoints;

            var chance = _curves.Get(narrator.EventChanceCurves, newPoints);

            if (_random.NextFloat() >= chance)
                continue;

            var points = EventPoints(settlement, rule.Comp.Narrator);

            if (PickRandomEvent(settlement, rule) is { } ev)
            {
                _adminLogger.Add(
                    LogType.EventRan,
                    LogImpact.Low,
                    $"Narrator: {ToPrettyString(settlement)}: the chance {chance:0.00} has come up, the narrator has {points} points and chooses {ev}");
                StartEvent(settlement, ev, rule.Owner);
            }
            else
            {
                _adminLogger.Add(
                    LogType.EventRan,
                    LogImpact.Low,
                    $"Narrator: {ToPrettyString(settlement)}: the chance {chance:0.00} has come up, but no event can be afforded with {points} points ({settlement.Comp.ResidentCount} residents)");
            }
        }
    }

    // Global world events.
    private void UpdateGlobal(Entity<NarratorRuleComponent> rule, NarratorPrototype proto)
    {
        var comp = rule.Comp;

        if (comp.GlobalEvents == null)
            return;

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
                    || !ent.TryComp(out GlobalWorldRuleComponent? globalRule, EntityManager.ComponentFactory)
                    || chance > globalChance * globalRule.ChanceMod
                    || globalRule.Cost > globalPoints)
                    continue;

                available.Add(eventId);
            }

            if (available.Count > 0)
            {
                // The event is made of the points of the narrator that starts it.
                var ruleEntity = _ticker.AddGameRule(_random.Pick(available));
                Comp<GlobalWorldRuleComponent>(ruleEntity).Narrator = rule.Owner;
                _ticker.StartGameRule(ruleEntity);
                comp.LastEventTime = _ticker.RoundDuration();
                comp.LastWaitPoints = 0;
                return;
            }
        }

        comp.LastWaitPoints = globalWaitPoints;
    }

    /// <summary>
    /// Starts an event for a settlement, and the settlement starts waiting for the next one.
    /// </summary>
    /// <param name="settlement">The settlement.</param>
    /// <param name="proto">The event.</param>
    /// <param name="narrator">The narrator that starts it, whose points the event is made of. An event that is started without one
    /// has no points to be made of.</param>
    /// <param name="ignoreDelay">Start at once, whatever the delay of the event.</param>
    public bool StartEvent(
        Entity<SettlementComponent> settlement,
        EntProtoId proto,
        EntityUid? narrator = null,
        bool ignoreDelay = false)
    {
        if (!_eventsQuery.TryComp(settlement, out var events))
            return false;

        var pointsBefore = narrator is { } n && TryComp(n, out NarratorRuleComponent? narratorRule)
            ? EventPoints(settlement, narratorRule.Narrator)
            : -1;

        var ruleEntity = _rule.AddWorldRule(proto, settlement);
        Comp<WorldRuleComponent>(ruleEntity).Narrator = narrator;
        var started = $"{(narrator == null ? "no narrator" : "narrator " + ToPrettyString(narrator.Value))}, " +
                      $"the points of the settlement {pointsBefore}, {events.BonusPoints} of them added by hand";
        _adminLogger.Add(LogType.EventRan, LogImpact.Low, $"{ToPrettyString(ruleEntity)}: {started}");

        if (!_rule.StartWorldRule(ruleEntity, ignoreDelay))
        {
            Log.Warning($"{ToPrettyString(ruleEntity)} could not be started");
            _adminLogger.Add(LogType.EventRan, LogImpact.Medium, $"{ToPrettyString(ruleEntity)}: could not be started");
            return false;
        }

        // The points that were added by hand are spent as the others are.
        if (_prototype.Resolve(proto, out var prototype)
            && prototype.TryComp(out WorldRuleComponent? worldRule, EntityManager.ComponentFactory))
            events.BonusPoints = Math.Max(0, events.BonusPoints - worldRule.Cost);

        events.LastEventTime = _ticker.RoundDuration();
        events.LastWaitPoints = 0;
        return true;
    }

    /// <summary>
    /// Returns a random event that the narrator can trigger for the settlement.
    /// </summary>
    public EntProtoId? PickRandomEvent(Entity<SettlementComponent> settlement, Entity<NarratorRuleComponent> rule)
    {
        var points = EventPoints(settlement, rule.Comp.Narrator);
        var available = new List<EntProtoId>();

        foreach (var (proto, comp) in AvailableRules(settlement, rule.Comp))
        {
            if (comp.Cost <= points)
                available.Add(proto);
        }

        if (available.Count == 0)
            return null;

        return _random.Pick(available);
    }

    /// <summary>
    /// The events that can happen to the settlement at all, whatever the points: it is late enough in the round, the biome fits, the
    /// settlement has as many residents as the event needs.
    /// </summary>
    public List<(EntProtoId Proto, WorldRuleComponent Comp)> AvailableRules(
        Entity<SettlementComponent> settlement,
        NarratorRuleComponent rule)
    {
        var available = new List<(EntProtoId, WorldRuleComponent)>();

        if (rule.WorldEvents == null)
            return available;

        var coord = _settlements.GetCoordinates(settlement);

        if (_transform.GetGrid(coord) is not { } gridUid)
            return available;

        var indicates = _map.TileIndicesFor(gridUid, Comp<MapGridComponent>(gridUid), coord);

        foreach (var spawn in _table.GetSpawns(rule.WorldEvents))
        {
            if (!_prototype.Resolve(spawn, out var proto)
                || !proto.TryComp(out WorldRuleComponent? worldRule, EntityManager.ComponentFactory)
                || _ticker.RoundDuration() < worldRule.StartOffset
                || settlement.Comp.ResidentCount < worldRule.MinResidents
                || worldRule.MaxResidents is { } max && settlement.Comp.ResidentCount > max)
                continue;

            if (worldRule.RequiredBiomes != null
                && (!_biome.TryGetBiome(indicates, gridUid, out var biome)
                    || !worldRule.RequiredBiomes.Contains(biome)))
                continue;

            available.Add((spawn, worldRule));
        }

        return available;
    }

    private string DebugTextGlobal(NarratorRuleComponent rule)
    {
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

            events += $"- {eventId}: {(int) Math.Floor(chance * globalRule.ChanceMod * 100)}%\n";
        }

        return "========GLOBAL EVENTS INFO========\n" +
               $"Event wait points: {waitPoints}\n" +
               $"Round time seconds: {_ticker.RoundDuration().TotalSeconds}\n" +
               $"Narrator mood: {narratorMood}\n" +
               $"Event points: {waitPoints * narratorMood}\n" +
               $"Current event chance(0-1): {chance}\n" +
               $"Available events: \n{events}";
    }

    private string DebugText(Entity<SettlementComponent> settlement, NarratorRuleComponent rule)
    {
        if (!_prototype.TryIndex(rule.Narrator, out var narrator) || !_eventsQuery.TryComp(settlement, out var settlementEvents))
            return "Unknown";

        var waitPoints = WaitPoint(narrator, settlementEvents);
        var narratorMood = _curves.Get(narrator.MoodCurves);
        var chance = _curves.Get(narrator.EventChanceCurves, waitPoints);
        var wealth = SettlementWealth(settlement, rule.Narrator);
        var wealthFactor = _curves.Get(narrator.WealthCurves, wealth);
        var points = EventPoints(settlement, rule.Narrator);

        var events = "";

        foreach (var (eventId, comp) in AvailableRules(settlement, rule))
        {
            events += $"- {eventId}: cost {comp.Cost}{(comp.Cost > points ? " (not enough points)" : string.Empty)}\n";
        }

        var categories = "";

        if (_wealthQuery.TryComp(settlement, out var wealthComp))
        {
            foreach (var (category, value) in wealthComp.Values)
            {
                categories += $"  {category}: {value:0} counts for {CategoryWealth(narrator, category, value):0} ({wealthComp.Counts.GetValueOrDefault(category)} things)\n";
            }
        }

        return "=================================\n" +
               $"Settlement: {ToPrettyString(settlement)} (player: {ToPrettyString(settlement.Comp.Player)})\n" +
               $"Settlement wealth: {wealth}\n{categories}" +
               $"Wealth points: {wealthFactor}\n" +
               $"Event wait points: {waitPoints}\n" +
               $"Bonus points: {settlementEvents.BonusPoints}\n" +
               $"Round time seconds: {_ticker.RoundDuration().TotalSeconds}\n" +
               $"Narrator mood: {narratorMood}\n" +
               $"Event points: {points}\n" +
               $"Current event chance(0-1): {chance}\n" +
               $"Available events: \n{events}";
    }
}
