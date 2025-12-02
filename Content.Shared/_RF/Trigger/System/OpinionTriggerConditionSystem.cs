using Content.Shared._RF.Socialization;
using Content.Shared._RF.Trigger.Components;
using Content.Shared.Trigger;

namespace Content.Shared._RF.Trigger.System;

public sealed class OpinionTriggerConditionSystem : EntitySystem
{
    [Dependency] private readonly SocializationSystem _socialization = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<OpinionTriggerConditionComponent, AttemptTriggerEvent>(OnAttemptTrigger);
    }

    private void OnAttemptTrigger(Entity<OpinionTriggerConditionComponent> ent, ref AttemptTriggerEvent args)
    {
        if (args.Key == null || !ent.Comp.Keys.Contains(args.Key))
            return;

        if (args.User == null)
        {
            args.Cancelled = true;
            return;
        }

        var opinion = _socialization.GetOpinion(ent.Owner, args.User.Value);

        if ((ent.Comp.Min == null || ent.Comp.Min > opinion)
            && (ent.Comp.Max == null || ent.Comp.Max < opinion))
            args.Cancelled = true;
    }
}
