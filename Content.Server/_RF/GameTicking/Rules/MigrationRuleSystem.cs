using Content.Shared.Trigger.Systems;
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
    [Dependency] private IPrototypeManager _prototype = null!;
    [Dependency] private ExecutableGoalSystem _executable = null!;
    [Dependency] private NarratorSystem _narrator = null!;
    [Dependency] private TriggerSystem _trigger = null!;


    /// <summary>
    /// The key of the trigger of the rule for every entity that it has spawned, the user is the entity.
    /// </summary>
    public const string Spawned = "spawned";

    /// <summary>
    /// The key of the trigger of the rule when it has spawned all of them, with no user. It is for what depends on all of them
    /// being there, such as the squads.
    /// </summary>
    public const string SpawnedAll = "spawned.all";

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

        // What is to happen to every one of them is said by what reacts to the trigger, such as joining a situation.
        foreach (var pop in pops)
        {
            _trigger.Trigger(ent, pop, Spawned, predicted: false);
        }

        _trigger.Trigger(ent, key: SpawnedAll, predicted: false);

        if (!_prototype.Resolve(ent.Comp.Goal, out var task))
            return;

        foreach (var pop in pops)
        {
            _executable.TrySetGoal(pop, task, targetCoords: args.TargetCoordinates);
        }
    }
}
