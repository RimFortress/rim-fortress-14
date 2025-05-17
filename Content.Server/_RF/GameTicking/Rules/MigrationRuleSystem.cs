using Content.Server._RF.World;
using Content.Server.GameTicking.Rules;
using Content.Server.Parallax;
using Content.Shared._RF.GameTicking.Rules;
using Content.Shared._RF.World;
using Content.Shared.GameTicking.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Map.Components;
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
    [Dependency] private readonly BiomeSystem _biome = default!;
    [Dependency] private readonly MapSystem _map = default!;

    protected override void Started(EntityUid uid, MigrationRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        if (!_rimRule.TryGetEvent(out var gridUid, out var coords, out var player))
            return;

        var indicates = _map.TileIndicesFor(gridUid.Value, Comp<MapGridComponent>(gridUid.Value), coords.Value);

        if (component.RequiredBiomes.Count != 0
            && (!_biome.TryGetBiome(indicates, gridUid.Value, out var biome)
                || !component.RequiredBiomes.Contains(biome)))
        {
            // If the map does not match the requirement, call the event again.
            // This event will not be called again, because the timer has been updated
            RaiseLocalEvent(new PlayerAvailableForEvent { Player = player.Value });
            return;
        }

        var spawn = _random.Pick(component.Spawn);
        var pops = _world.SpawnPop(coords.Value, spawn, amount: component.Amount.Next(_random));

        if (component.AddToPops)
            _world.AddPops((player.Value.Owner, player.Value.Comp), pops);
    }
}
