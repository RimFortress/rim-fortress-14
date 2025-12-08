using Content.Shared._RF.Socialization.Components;
using Content.Shared.Nutrition.EntitySystems;

namespace Content.Shared._RF.Socialization.Systems;

public sealed class ChangeMoodOnThirstSystem : EntitySystem
{
    [Dependency] private readonly SocializationSystem _socialization = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<ChangeMoodOnThirstComponent, ThirstThresholdChangedEvent>(OnThirstThresholdChanged);
    }

    private void OnThirstThresholdChanged(EntityUid uid, ChangeMoodOnThirstComponent component, ThirstThresholdChangedEvent args)
    {
        if (!component.Effects.TryGetValue(args.Current, out var effects))
            return;

        foreach (var effect in effects)
        {
            _socialization.AddMoodEffect(uid, effect);
        }
    }
}
