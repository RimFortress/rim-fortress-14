using Content.Shared._RF.Trigger.Components;
using Content.Shared.Damage;
using Content.Shared.Trigger.Systems;

namespace Content.Shared._RF.Trigger.System;

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
