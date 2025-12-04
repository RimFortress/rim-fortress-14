using Content.Shared._RF.Trigger.Components;
using Content.Shared.Trigger;

namespace Content.Shared._RF.Trigger.Systems;

public sealed class TriggerMappingsSystem : EntitySystem
{
    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<TriggerMappingsComponent, AttemptTriggerEvent>(OnAttemptTrigger);
    }

    private void OnAttemptTrigger(Entity<TriggerMappingsComponent> ent, ref AttemptTriggerEvent args)
    {
        if (args.Key == null || !ent.Comp.Mappings.TryGetValue(args.Key, out var components))
            return;

        EntityManager.AddComponents(ent, components, ent.Comp.Override);
    }
}
