using Content.Shared._RF.Social.Components;
using Content.Shared.Nutrition.EntitySystems;

namespace Content.Shared._RF.Social.Systems;

public sealed class ChangeMoodOnHungerSystem : EntitySystem
{
    [Dependency] private readonly SocialSystem _social = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<ChangeMoodOnHungerComponent, HungerThresholdChangedEvent>(OnHungerThresholdChanged);
    }

    private void OnHungerThresholdChanged(EntityUid uid, ChangeMoodOnHungerComponent component, HungerThresholdChangedEvent args)
    {
        if (component.Effects.TryGetValue(args.Current, out var effects))
            _social.AddMoodEffect(uid, effects);

        if (component.RemoveEffects.TryGetValue(args.Current, out var removeEffects))
            _social.RemoveMoodEffect(uid, removeEffects);
    }
}
