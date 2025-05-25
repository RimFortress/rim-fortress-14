using Content.Server._RF.World;
using Content.Shared._RF.GameTicking.Rules;
using Robust.Shared.Random;

namespace Content.Server._RF.GameTicking.Rules;

/// <summary>
/// Manages <see cref="MigrationRuleComponent"/>
/// </summary>
public sealed class MigrationRuleSystem : WorldRuleSystem<MigrationRuleComponent>
{
    [Dependency] private readonly RimFortressWorldSystem _world = default!;

    protected override void Started(EntityUid uid, MigrationRuleComponent component, WorldRuleComponent worldRule, WorldRuleStartedEvent args)
    {
        var spawn = Random.Pick(component.Spawn);
        var pops = _world.SpawnPop(args.TargetCoordinates, spawn, amount: component.Amount.Next(Random));

        if (component.AddToPops)
            _world.AddPops(args.Target, pops);
    }
}
