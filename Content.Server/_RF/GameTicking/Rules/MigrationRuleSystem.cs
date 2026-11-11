using Content.Server.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Trigger.Systems;
using Content.Server._RF.Narrator;
using Content.Server._RF.NPC.Executable.Systems;
using System.Linq;
using Content.Shared._RF.GameTicking.Rules;
using Content.Shared._RF.Narrator;
using Content.Shared._RF.World.Settlements;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._RF.GameTicking.Rules;

/// <summary>
/// Manages <see cref="MigrationRuleComponent"/>
/// </summary>
public sealed partial class MigrationRuleSystem : WorldRuleSystem<MigrationRuleComponent>
{
    [Dependency] private IPrototypeManager _prototype = null!;
    [Dependency] private ExecutableGoalSystem _executable = null!;
    [Dependency] private NarratorSystem _narrator = null!;
    [Dependency] private TriggerSystem _trigger = null!;
    [Dependency] private EventBudgetSystem _budget = null!;
    [Dependency] private IAdminLogManager _adminLogger = null!;

    [Dependency] private EntityQuery<SettlementComponent> _settlementQuery;

    /// <summary>
    /// The key of the trigger of the rule for every entity that it has spawned, the user is the entity.
    /// </summary>
    public const string Spawned = "spawned";

    protected override void Started(Entity<MigrationRuleComponent> ent, WorldRuleComponent worldRule, WorldRuleStartedEvent args)
    {
        // The points of the event are those that its narrator gives.
        if (worldRule.Narrator is not { } narrator || !TryComp(narrator, out NarratorRuleComponent? rule))
        {
            Warn(ent, "nobody came: the event was started without a narrator, so it has no points to be made of");
            return;
        }

        // We spawn one entity anyway, regardless of points, so as not to trigger the event for nothing
        var spawn = new List<EntProtoId> { Random.Pick(ent.Comp.Spawn.Keys) };

        if (!_settlementQuery.TryComp(args.Settlement, out var settlement))
        {
            Warn(ent, $"nobody came: {ToPrettyString(args.Settlement)} is not a settlement");
            return;
        }

        var eventPoints = _narrator.EventPoints((args.Settlement, settlement), rule.Narrator);
        var points = eventPoints - worldRule.Cost;

        // A part of the points is kept for the gear of those who come.
        var purchasePoints = ent.Comp.Purchases.Count == 0 ? 0 : (int) (Math.Max(0, points) * Math.Clamp(ent.Comp.PurchaseShare.Next(Random), 0f, 1f));
        points -= purchasePoints;

        // The first one is there anyway; the rest is bought, as many as the limit lets.
        if (ent.Comp.MaxSpawn == 0 || spawn.Count < ent.Comp.MaxSpawn)
        {
            var max = ent.Comp.MaxSpawn == 0 ? 0 : ent.Comp.MaxSpawn - spawn.Count;
            spawn.AddRange(_budget.Buy(ref points, ent.Comp.Spawn.Select(x => (x.Key, x.Value, 1f)), max));
        }

        var plan = $"points: the settlement has {eventPoints}, the event costs {worldRule.Cost}, " +
                   $"{purchasePoints} are kept for the purchases, {points} are for the ones who come; comes: {string.Join(", ", spawn)}";
        _adminLogger.Add(LogType.EventRan, LogImpact.Low, $"{ToPrettyString(ent)}: {plan}");

        var pops = World.SpawnPop(args.TargetCoordinates, spawn, ent.Comp.RadiusFromSettlement);

        if (pops.Count == 0)
            Warn(ent, "nobody came: there is no place to spawn them (the tiles near the settlement are all taken or too close to it)");
        else
            _adminLogger.Add(LogType.EventRan, LogImpact.Low, $"{ToPrettyString(ent)}: came: {string.Join(", ", pops.Select(x => $"{ToPrettyString(x)} at {Transform(x).Coordinates}"))}");

        if (ent.Comp.AddToPops)
            World.AddPops(settlement.Player, pops);

        if (ent.Comp.Purchases.Count > 0)
        {
            var left = purchasePoints + Math.Max(0, points);
            var result = _budget.Spend(left, ent.Comp.Purchases, pops, ent);
            var bought = $"purchases: {left} points, {result.Bought.Count} bought, {result.Left} left" +
                         (result.Bought.Count == 0 ? string.Empty : ": " + string.Join(", ", result.Bought.Select(x => $"{x.Purchase} for {ToPrettyString(x.Target)}")));
            _adminLogger.Add(LogType.EventRan, LogImpact.Low, $"{ToPrettyString(ent)}: {bought}");
        }

        // What is to happen to every one of them is said by what reacts to the trigger, such as joining a situation.
        foreach (var pop in pops)
        {
            _trigger.Trigger(ent, pop, Spawned, predicted: false);
        }

        if (!_prototype.Resolve(ent.Comp.Goal, out var task))
            return;

        foreach (var pop in pops)
        {
            _executable.TrySetGoal(pop, task, targetCoords: args.TargetCoordinates);
        }
    }

    private void Warn(EntityUid rule, string text)
    {
        Log.Warning($"{ToPrettyString(rule)}: {text}");
        _adminLogger.Add(LogType.EventRan, LogImpact.Medium, $"{ToPrettyString(rule)}: {text}");
    }
}
