using Content.Shared._RF.Trigger.Components.Triggers;
using Content.Shared.Medical;
using Content.Shared.Medical.Healing;
using Content.Shared.Trigger.Systems;

namespace Content.Shared._RF.Trigger.Systems;

public sealed partial class TriggerOnHealSystem : EntitySystem
{
    [Dependency] private TriggerSystem _trigger = default!;

    [SubscribeLocalEvent(after: new[] { typeof(HealingSystem) })]
    private void OnDoAfter(Entity<TriggerOnHealComponent> ent, ref HealingDoAfterEvent args)
    {
        if (args.Cancelled || !args.Handled)
            return;

        _trigger.Trigger(ent, args.User, ent.Comp.KeyOut);
    }
}
