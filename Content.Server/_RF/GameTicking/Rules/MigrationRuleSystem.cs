using Content.Server._RF.Narrator;
using Content.Server._RF.NPC.Systems;
using Content.Shared._RF.GameTicking.Rules;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._RF.GameTicking.Rules;

/// <summary>
/// Manages <see cref="MigrationRuleComponent"/>
/// </summary>
public sealed class MigrationRuleSystem : WorldRuleSystem<MigrationRuleComponent>
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly ExecutableGoalSystem _executable = default!;
    [Dependency] private readonly NarratorSystem _narrator = default!;

    protected override void Started(EntityUid uid, MigrationRuleComponent component, WorldRuleComponent worldRule, WorldRuleStartedEvent args)
    {
        if (Rule.GetRule() is not { } rule)
            return;

        // We spawn one entity anyway, regardless of points, so as not to trigger the event for nothing
        var spawn = new List<EntProtoId> { Random.Pick(component.Spawn.Keys) };
        var points = _narrator.EventPoints(args.Target, rule.Narrator, args.TargetCoordinates) - worldRule.Cost;

        while (true)
        {
            if (component.MaxSpawn != 0 && spawn.Count >= component.MaxSpawn)
                break;

            var available = new List<EntProtoId>();

            foreach (var (proto, cost) in component.Spawn)
            {
                if (cost > points)
                    continue;

                available.Add(proto);
            }

            if (available.Count == 0)
                break;

            var entProto = Random.Pick(available);
            spawn.Add(entProto);
            points -= component.Spawn[entProto];
        }

        var pops = World.SpawnPop(args.TargetCoordinates, spawn, component.RadiusFromSettlement);

        if (component.AddToPops)
            World.AddPops(args.Target, pops);

        if (!_prototype.Resolve(component.Goal, out var task))
            return;

        foreach (var pop in pops)
        {
            _executable.TrySetGoal(pop, task, targetCoords: args.TargetCoordinates);
        }
    }
}
