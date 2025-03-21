using Content.Server._RF.Procedural;
using Content.Server.GameTicking.Rules;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;

namespace Content.Server._RF.GameTicking.Rules;

/// <summary>
/// Manages <see cref="RimFortressRuleComponent"/>
/// </summary>
public sealed class RimFortressRuleSystem : GameRuleSystem<RimFortressRuleComponent>
{
    [Dependency] private readonly RimFortressWorldSystem _world = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerBeforeSpawnEvent>(OnBeforeSpawn);
    }

    protected override void Added(EntityUid uid, RimFortressRuleComponent comp, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        base.Added(uid, comp, gameRule, args);

        _world.InitializeWorld(comp.WorldSize);
    }

    private void OnBeforeSpawn(PlayerBeforeSpawnEvent ev)
    {
        var query = EntityQueryEnumerator<RimFortressRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var rf, out var rule))
        {
            if (!GameTicker.IsGameRuleActive(uid, rule))
                continue;

            _world.CreateOwnerMap(ev.Player, rf);

            ev.Handled = true;
            return;
        }
    }
}
