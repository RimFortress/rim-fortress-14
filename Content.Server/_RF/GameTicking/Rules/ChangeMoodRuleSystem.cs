using Content.Shared._RF.GameTicking.Rules;
using Content.Shared._RF.Social.Systems;
using Content.Shared._RF.World.Settlements;

namespace Content.Server._RF.GameTicking.Rules;

/// <summary>
/// Manges <see cref="ChangeMoodRuleComponent"/>
/// </summary>
public sealed partial class ChangeMoodRuleSystem : WorldRuleSystem<ChangeMoodRuleComponent>
{
    [Dependency] private SocialSystem _social = default!;

    [Dependency] private EntityQuery<SettlementComponent> _settlementQuery;

    protected override void Started(
        Entity<ChangeMoodRuleComponent> ent,
        WorldRuleComponent worldRule,
        WorldRuleStartedEvent args)
    {
        if (!_settlementQuery.TryComp(args.Settlement, out var settlement))
            return;

        foreach (var popUid in settlement.Residents)
        {
            _social.AddMoodEffect(popUid, ent.Comp.Effects);
            _social.RemoveMoodEffect(popUid, ent.Comp.RemoveEffects);
        }
    }
}
