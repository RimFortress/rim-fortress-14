using Content.Shared._RF.GameTicking.Rules;
using Content.Shared._RF.Social.Systems;

namespace Content.Server._RF.GameTicking.Rules;

/// <summary>
/// Manges <see cref="ChangeMoodRuleComponent"/>
/// </summary>
public sealed class ChangeMoodRuleSystem : WorldRuleSystem<ChangeMoodRuleComponent>
{
    [Dependency] private readonly SocialSystem _social = default!;

    protected override void Started(EntityUid uid,
        ChangeMoodRuleComponent component,
        WorldRuleComponent worldRule,
        WorldRuleStartedEvent args)
    {
        foreach (var popUid in World.GetPLayerPops(args.Target) ?? new())
        {
            _social.AddMoodEffect(popUid, component.Effects);
            _social.RemoveMoodEffect(popUid, component.RemoveEffects);
        }
    }
}
