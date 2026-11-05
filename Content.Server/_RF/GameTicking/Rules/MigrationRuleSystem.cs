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

    [Dependency] private EntityQuery<SettlementComponent> _settlementQuery;

    /// <summary>
    /// The key of the trigger of the rule for every entity that it has spawned, the user is the entity.
    /// </summary>
    public const string Spawned = "spawned";

    protected override void Started(Entity<MigrationRuleComponent> ent, WorldRuleComponent worldRule, WorldRuleStartedEvent args)
    {
        // The points of the event are those that its narrator gives.
        if (worldRule.Narrator is not { } narrator || !TryComp(narrator, out NarratorRuleComponent? rule))
            return;

        // We spawn one entity anyway, regardless of points, so as not to trigger the event for nothing
        var spawn = new List<EntProtoId> { Random.Pick(ent.Comp.Spawn.Keys) };

        if (!_settlementQuery.TryComp(args.Settlement, out var settlement))
            return;

        var points = _narrator.EventPoints((args.Settlement, settlement), rule.Narrator) - worldRule.Cost;

        // A part of the points is kept for the gear of those who come.
        var purchasePoints = ent.Comp.Purchases.Count == 0 ? 0 : (int) (Math.Max(0, points) * Math.Clamp(ent.Comp.PurchaseShare.Next(Random), 0f, 1f));
        points -= purchasePoints;

        // The first one is there anyway; the rest is bought, as many as the limit lets.
        if (ent.Comp.MaxSpawn == 0 || spawn.Count < ent.Comp.MaxSpawn)
        {
            var max = ent.Comp.MaxSpawn == 0 ? 0 : ent.Comp.MaxSpawn - spawn.Count;
            spawn.AddRange(_budget.Buy(ref points, ent.Comp.Spawn.Select(x => (x.Key, x.Value, 1f)), max));
        }

        var pops = World.SpawnPop(args.TargetCoordinates, spawn, ent.Comp.RadiusFromSettlement);

        if (ent.Comp.AddToPops)
            World.AddPops(settlement.Player, pops);

        if (ent.Comp.Purchases.Count > 0)
            _budget.Spend(purchasePoints + Math.Max(0, points), ent.Comp.Purchases, pops, ent);

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
}
