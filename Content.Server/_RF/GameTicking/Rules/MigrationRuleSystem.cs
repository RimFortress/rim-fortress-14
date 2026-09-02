using Content.Server._RF.Narrator;
using Content.Server._RF.NPC.Executable.Systems;
using Content.Shared._RF.GameTicking.Rules;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._RF.GameTicking.Rules;

/// <summary>
/// Manages <see cref="MigrationRuleComponent"/>
/// </summary>
public sealed partial class MigrationRuleSystem : WorldRuleSystem<MigrationRuleComponent>
{
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private ExecutableGoalSystem _executable = default!;
    [Dependency] private NarratorSystem _narrator = default!;

    protected override void Started(Entity<MigrationRuleComponent> ent, WorldRuleComponent worldRule, WorldRuleStartedEvent args)
    {
        if (Rule.GetRule() is not { } rule)
            return;

        // We spawn one entity anyway, regardless of points, so as not to trigger the event for nothing
        var spawn = new List<EntProtoId> { Random.Pick(ent.Comp.Spawn.Keys) };
        var points = _narrator.EventPoints(args.Target, rule.Narrator, args.TargetCoordinates) - worldRule.Cost;

        while (true)
        {
            if (ent.Comp.MaxSpawn != 0 && spawn.Count >= ent.Comp.MaxSpawn)
                break;

            var available = new List<EntProtoId>();

            foreach (var (proto, cost) in ent.Comp.Spawn)
            {
                if (cost > points)
                    continue;

                available.Add(proto);
            }

            if (available.Count == 0)
                break;

            var entProto = Random.Pick(available);
            spawn.Add(entProto);
            points -= ent.Comp.Spawn[entProto];
        }

        var pops = World.SpawnPop(args.TargetCoordinates, spawn, ent.Comp.RadiusFromSettlement);

        if (ent.Comp.AddToPops)
            World.AddPops(args.Target, pops);

        if (!_prototype.Resolve(ent.Comp.Goal, out var task))
            return;

        foreach (var pop in pops)
        {
            _executable.TrySetGoal(pop, task, targetCoords: args.TargetCoordinates);
        }
    }
}
