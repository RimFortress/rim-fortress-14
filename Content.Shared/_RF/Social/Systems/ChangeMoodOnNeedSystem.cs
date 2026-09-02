using Content.Shared._RF.Needs.Systems;
using Content.Shared._RF.Social.Components;

namespace Content.Shared._RF.Social.Systems;

/// <summary>
/// Manages <see cref="ChangeMoodOnAteComponent"/>
/// </summary>
public sealed partial class ChangeMoodOnNeedSystem : EntitySystem
{
    [Dependency] private SocialSystem _social = default!;

    [SubscribeLocalEvent]
    private void OnNeedThresholdChanged(Entity<ChangeMoodOnNeedComponent> ent, ref NeedThresholdChangedEvent args)
    {
        if (ent.Comp.Effects.TryGetValue(args.Need, out var thresholds)
            && thresholds.TryGetValue(args.New, out var effects))
            _social.AddMoodEffect(ent.Owner, effects);

        if (ent.Comp.RemoveEffects.TryGetValue(args.Need, out var removeThresholds)
            && removeThresholds.TryGetValue(args.New, out var removeEffects))
            _social.RemoveMoodEffect(ent.Owner, removeEffects);
    }
}
