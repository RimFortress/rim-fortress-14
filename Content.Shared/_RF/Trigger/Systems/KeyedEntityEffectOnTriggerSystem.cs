using Content.Shared._RF.MathHelpers.MathVariables;
using Content.Shared._RF.Phases;
using Content.Shared._RF.Trigger.Components.Effects;
using Content.Shared.EntityEffects;
using Content.Shared.Trigger;

namespace Content.Shared._RF.Trigger.Systems;

public sealed partial class KeyedEntityEffectOnTriggerSystem : EntitySystem
{
    [Dependency] private SharedEntityEffectsSystem _effects = null!;

    // After the metrics of the entity are calculated, and the phases have decided what is next.
    [SubscribeLocalEvent(after: [typeof(MathVariablesSystem), typeof(PhasesSystem)])]
    private void OnTrigger(Entity<KeyedEntityEffectOnTriggerComponent> ent, ref TriggerEvent args)
    {
        var target = ent.Comp.TargetUser ? args.User : ent.Owner;

        if (target is not { } uid)
            return;

        if (args.Key is { } key)
        {
            if (!ent.Comp.Effects.TryGetValue(key, out var effects))
                return;

            _effects.ApplyEffects(uid, effects, ent.Comp.Scale, args.User);
            args.Handled = true;
            return;
        }

        foreach (var effects in ent.Comp.Effects.Values)
        {
            _effects.ApplyEffects(uid, effects, ent.Comp.Scale, args.User);
        }

        args.Handled = true;
    }
}
