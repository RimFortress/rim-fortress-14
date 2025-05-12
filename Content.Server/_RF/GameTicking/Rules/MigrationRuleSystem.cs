using Content.Server._RF.World;
using Content.Server.GameTicking.Rules;
using Content.Shared._RF.GameTicking.Rules;
using Content.Shared._RF.World;
using Content.Shared.GameTicking.Components;
using Content.Shared.Parallax.Biomes;
using Robust.Shared.Random;

namespace Content.Server._RF.GameTicking.Rules;

/// <summary>
/// Manages <see cref="MigrationRuleComponent"/>
/// </summary>
public sealed class MigrationRuleSystem : GameRuleSystem<MigrationRuleComponent>
{
    [Dependency] private readonly RimFortressWorldSystem _world = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly RimFortressRuleSystem _rimRule = default!;

    protected override void Started(EntityUid uid, MigrationRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        if (!_rimRule.TryGetEvent(out var coords, out var player))
            return;

        /* TODO
        if (component.RequiredBiomes.Count != 0
            && TryComp(map, out BiomeComponent? biome)
            && biome.Template is { } template
            && !component.RequiredBiomes.Contains(template))
        {
            // If the map does not match the requirement, call the event again.
            // This event will not be called again, because the timer has been updated
            RaiseLocalEvent(new WorldMapAvailableForEvent { Map = map });
            return;
        }
        */

        var spawn = _random.Pick(component.Spawn);
        var pops = _world.SpawnPop(coords.Value, spawn, amount: component.Amount.Next(_random));

        if (component.AddToPops)
            _world.AddPops((player.Value.Owner, player.Value.Comp), pops);
    }
}
