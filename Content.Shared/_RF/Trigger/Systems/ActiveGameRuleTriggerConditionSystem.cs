using Content.Shared._RF.Trigger.Components.Conditions;
using Content.Shared.GameTicking.Components;
using Content.Shared.Trigger;

namespace Content.Shared._RF.Trigger.Systems;

public sealed partial class ActiveGameRuleTriggerConditionSystem : EntitySystem
{
    [Dependency] private EntityQuery<ActiveGameRuleComponent> _activeQuery;

    [SubscribeLocalEvent]
    private void OnAttempt(Entity<ActiveGameRuleTriggerConditionComponent> ent, ref AttemptTriggerEvent args)
    {
        if (args.Key != null && !ent.Comp.Keys.Contains(args.Key))
            return;

        args.Cancelled |= !_activeQuery.HasComp(ent);
    }
}
