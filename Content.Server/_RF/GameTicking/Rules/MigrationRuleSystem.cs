using Content.Server._RF.NPC;
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
    [Dependency] private readonly NpcControlSystem _control = default!;

    protected override void Started(EntityUid uid, MigrationRuleComponent component, WorldRuleComponent worldRule, WorldRuleStartedEvent args)
    {
        var amount = component.Amount.Next(Random);
        var spawn = new List<EntProtoId>();

        for (var i = 0; i < amount; i++)
        {
            spawn.Add(Random.Pick(component.Spawn));
        }

        var pops = World.SpawnPop(args.TargetCoordinates, spawn, component.RadiusFromSettlement);

        if (component.AddToPops)
            World.AddPops(args.Target, pops);

        if (!_prototype.TryIndex(component.Task, out var task))
            return;

        foreach (var pop in pops)
        {
            _control.TrySetTask(pop, task, args.TargetCoordinates);
        }
    }
}
