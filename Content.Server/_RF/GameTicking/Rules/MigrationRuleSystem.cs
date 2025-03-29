using Content.Server._RF.World;
using Content.Server.GameTicking.Rules;
using Content.Shared._RF.GameTicking.Rules;
using Content.Shared._RF.World;
using Content.Shared.GameTicking.Components;
using Content.Shared.NPC.Systems;
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

        var spawn = _random.Pick(component.Spawn);
        var pops = _world.SpawnPopAlongWall(map, 5, spawn, amount: _random.Next(component.Min, component.Max));

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
