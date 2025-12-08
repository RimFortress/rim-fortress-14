using Content.Shared._RF.Trigger.Components;
using Content.Shared._RF.Trigger.Systems;
using Content.Shared.Trigger;
using Content.Shared.Trigger.Systems;

namespace Content.Server._RF.Trigger.Systems;

public sealed class TriggerMappingsSystem : EntitySystem
{
    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<TriggerMappingsComponent, AttemptTriggerEvent>(OnAttemptTrigger,
            before: new[] { typeof(TriggerSystem), typeof(SocialTriggerSystem), typeof(TriggerConditionsSystem) });
    }

    private void OnAttemptTrigger(Entity<TriggerMappingsComponent> ent, ref AttemptTriggerEvent args)
    {
        if (args.Cancelled || args.Key == null || !ent.Comp.AddComponents.TryGetValue(args.Key, out var components))
            return;

        EntityManager.AddComponents(ent, components, ent.Comp.Override);
    }
}
