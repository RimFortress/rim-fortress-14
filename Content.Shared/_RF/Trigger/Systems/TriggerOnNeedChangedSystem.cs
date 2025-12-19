using Content.Shared._RF.Needs.Systems;
using Content.Shared._RF.Trigger.Components.Triggers;
using Content.Shared.Trigger.Systems;

namespace Content.Shared._RF.Trigger.Systems;

public sealed class TriggerOnNeedChangedSystem : EntitySystem
{
    [Dependency] private readonly TriggerSystem _trigger = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<TriggerOnNeedChangedComponent, NeedThresholdChangedEvent>(OnNeedThresholdChanged);
    }

    private void OnNeedThresholdChanged(
        EntityUid uid,
        TriggerOnNeedChangedComponent component,
        NeedThresholdChangedEvent args)
    {
        if (args.Need != component.Need)
            return;

        if (component.Threshold != null && component.Threshold != args.New)
            return;

        _trigger.Trigger(uid, key: component.KeyOut);
    }
}
