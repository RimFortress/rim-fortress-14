using Content.Server._RF.World;
using Content.Server.GameTicking.Rules;
using Content.Shared._RF.GameTicking.Rules;
using Content.Shared._RF.World;
using Content.Shared.GameTicking.Components;
using Content.Shared.NPC.Systems;
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
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly RimFortressRuleSystem _rimRule = default!;

    protected override void Added(EntityUid uid, MigrationRuleComponent component, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        base.Added(uid, component, gameRule, args);

        if (_rimRule.GetWorldMap() is not { } map)
            return;

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

        var spawn = _random.Pick(component.Spawn);
        var pops = _world.SpawnPopAlongBounds(
            map,
            component.ChunkSize,
            spawn,
            amount: component.Amount.Next(_random));

        if (map.Comp.OwnerPlayer is not { } playerUid
            || !TryComp(playerUid, out RimFortressPlayerComponent? playerComp))
            return;

        playerComp.Pops.AddRange(pops);

        if (component.AddToPlayerFaction)
        {
            _faction.AddFaction((playerUid, null), playerComp.Faction);
        }
    }
}
