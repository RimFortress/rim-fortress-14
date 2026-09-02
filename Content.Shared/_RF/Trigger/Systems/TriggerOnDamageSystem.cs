using Content.Shared._RF.Trigger.Components.Triggers;
using Content.Shared.Damage.Systems;
using Content.Shared.Trigger.Systems;

namespace Content.Shared._RF.Trigger.Systems;

public sealed partial class TriggerOnDamageSystem : EntitySystem
{
    [Dependency] private readonly TriggerSystem _trigger  = default!;

    [SubscribeLocalEvent]
    private void OnDamageChanged(Entity<TriggerOnDamageComponent> ent, ref DamageDealtEvent args)
    {
        _trigger.Trigger(ent, args.Origin, ent.Comp.KeyOut);
    }
}
