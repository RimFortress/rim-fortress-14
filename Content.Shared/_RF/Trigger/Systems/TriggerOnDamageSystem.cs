using Content.Shared._RF.Trigger.Components.Triggers;
using Content.Shared.Damage.Systems;
using Content.Shared.Trigger.Systems;

namespace Content.Shared._RF.Trigger.Systems;

public sealed class TriggerOnDamageSystem : EntitySystem
{
    [Dependency] private readonly TriggerSystem _trigger  = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<TriggerOnDamageComponent, DamageChangedEvent>(OnDamageChanged);
    }

    private void OnDamageChanged(EntityUid uid, TriggerOnDamageComponent component, DamageChangedEvent args)
    {
        _trigger.Trigger(uid, args.Origin, component.KeyOut);
    }
}
