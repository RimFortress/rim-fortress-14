using Content.Shared._RF.Socialization;
using Content.Shared._RF.Trigger.Components;
using Content.Shared.Trigger;

namespace Content.Shared._RF.Trigger.System;

public sealed class MoodTriggerConditionSystem : EntitySystem
{
    [Dependency] private readonly SocializationSystem _socialization = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<MoodTriggerConditionComponent, AttemptTriggerEvent>(OnAttemptTrigger);
    }

    private void OnAttemptTrigger(Entity<MoodTriggerConditionComponent> ent, ref AttemptTriggerEvent args)
    {
        var mood = _socialization.GetMood(ent.Owner);

        if (args.Key == null || ent.Comp.Keys.Contains(args.Key))
        {
            args.Cancelled |= (ent.Comp.Min == null || ent.Comp.Min > mood)
                              && (ent.Comp.Max == null || ent.Comp.Max < mood);
        }
    }
}
