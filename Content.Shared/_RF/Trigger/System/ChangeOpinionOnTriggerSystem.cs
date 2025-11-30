using Content.Shared._RF.Socialization;
using Content.Shared._RF.Trigger.Components;
using Content.Shared.Trigger;

namespace Content.Shared._RF.Trigger.System;

public sealed class ChangeOpinionOnTriggerSystem : EntitySystem
{
    [Dependency] private readonly SocializationSystem _socialization  = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<ChangeOpinionOnTriggerComponent, TriggerEvent>(OnTrigger);
    }

    private void OnTrigger(Entity<ChangeOpinionOnTriggerComponent> ent, ref TriggerEvent ev)
    {
        if (ev.User == null || ev.Key == null || !ent.Comp.KeysIn.Contains(ev.Key))
            return;

        if (ent.Comp.Effect != null)
        {
            if (ent.Comp.BothSide)
                _socialization.AddBothOpinionEffect(ent.Owner, ev.User.Value, ent.Comp.Effect.Value);
            else
                _socialization.AddOpinionEffect(ent.Owner, ev.User.Value, ent.Comp.Effect.Value);

            ev.Handled = true;
        }

        if (ent.Comp.RemovedEffect != null)
        {
            if (ent.Comp.BothSide)
                _socialization.RemoveBothOpinionEffect(ent.Owner, ev.User.Value, ent.Comp.RemovedEffect.Value);
            else
                _socialization.RemoveOpinionEffect(ent.Owner, ev.User.Value, ent.Comp.RemovedEffect.Value);

            ev.Handled = true;
        }
    }
}
