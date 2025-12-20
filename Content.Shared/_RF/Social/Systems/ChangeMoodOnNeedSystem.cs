using Content.Shared._RF.Needs.Systems;
using Content.Shared._RF.Social.Components;

namespace Content.Shared._RF.Social.Systems;

/// <summary>
/// Manages <see cref="ChangeMoodOnAteComponent"/>
/// </summary>
public sealed class ChangeMoodOnNeedSystem : EntitySystem
{
    [Dependency] private readonly SocialSystem _social = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<ChangeMoodOnNeedComponent, NeedThresholdChangedEvent>(OnNeedThresholdChanged);
    }

    private void OnNeedThresholdChanged(
        EntityUid uid,
        ChangeMoodOnNeedComponent component,
        NeedThresholdChangedEvent args)
    {
        if (component.Effects.TryGetValue(args.Need, out var thresholds)
            && thresholds.TryGetValue(args.New, out var effects))
            _social.AddMoodEffect(uid, effects);

        if (component.RemoveEffects.TryGetValue(args.Need, out var removeThresholds)
            && removeThresholds.TryGetValue(args.New, out var removeEffects))
            _social.RemoveMoodEffect(uid, removeEffects);
    }
}
