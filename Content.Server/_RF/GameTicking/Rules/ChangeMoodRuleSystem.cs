using Content.Shared._RF.GameTicking.Rules;
using Content.Shared._RF.Social.Systems;

namespace Content.Server._RF.GameTicking.Rules;

/// <summary>
/// Manges <see cref="ChangeMoodRuleComponent"/>
/// </summary>
public sealed partial class ChangeMoodRuleSystem : WorldRuleSystem<ChangeMoodRuleComponent>
{
    [Dependency] private SocialSystem _social = default!;

    protected override void Started(
        Entity<ChangeMoodRuleComponent> ent,
        WorldRuleComponent worldRule,
        WorldRuleStartedEvent args)
    {
        foreach (var popUid in World.GetPLayerPops(args.Target) ?? new())
        {
            _social.AddMoodEffect(popUid, ent.Comp.Effects);
            _social.RemoveMoodEffect(popUid, ent.Comp.RemoveEffects);
        }
    }
}
