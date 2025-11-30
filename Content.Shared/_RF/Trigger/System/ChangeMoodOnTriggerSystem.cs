using Content.Shared._RF.Socialization;
using Content.Shared._RF.Trigger.Components;
using Content.Shared.Trigger;

namespace Content.Shared._RF.Trigger.System;

public sealed class ChangeMoodOnTriggerSystem : EntitySystem
{
    [Dependency] private readonly SocializationSystem _socialization  = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<ChangeMoodOnTriggerComponent, TriggerEvent>(OnTrigger);
    }

    private void OnTrigger(EntityUid uid, ChangeMoodOnTriggerComponent component, TriggerEvent ev)
    {
        if (ev.Key == null || !component.KeysIn.Contains(ev.Key))
            return;

        if (component.Effect != null)
            _socialization.AddMoodEffect(uid, component.Effect.Value);

        if (component.RemovedEffect != null)
            _socialization.RemoveMoodEffect(uid, component.RemovedEffect.Value);

        ev.Handled = true;
    }
}
