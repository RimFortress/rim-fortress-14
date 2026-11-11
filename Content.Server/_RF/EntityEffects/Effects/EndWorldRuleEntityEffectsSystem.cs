using Content.Server._RF.GameTicking.Rules;
using Content.Shared._RF.EntityEffects.Effects;
using Content.Shared._RF.GameTicking.Rules;
using Content.Shared.EntityEffects;
using Content.Shared.GameTicking.Components;

namespace Content.Server._RF.EntityEffects.Effects;

public sealed partial class EndWorldRuleEntityEffectsSystem : EntityEffectSystem<WorldRuleComponent, EndWorldRule>
{
    [Dependency] private RimFortressRuleSystem _rule = null!;
    [Dependency] private EntityQuery<ActiveGameRuleComponent> _activeQuery;

    protected override void Effect(Entity<WorldRuleComponent> ent, ref EntityEffectEvent<EndWorldRule> args)
    {
        // A rule that is not active has nothing to end.
        if (_activeQuery.HasComp(ent))
            _rule.EndWorldRule(ent.AsNullable(), args.Effect.Result);
    }
}
