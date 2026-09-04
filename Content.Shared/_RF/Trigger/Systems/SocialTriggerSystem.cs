using Content.Shared._RF.Social.Systems;
using Content.Shared._RF.Trigger.Components.Conditions;
using Content.Shared._RF.Trigger.Components.Effects;
using Content.Shared.Trigger;

namespace Content.Shared._RF.Trigger.Systems;

public sealed partial class SocialTriggerSystem : EntitySystem
{
    [Dependency] private SocialSystem _social  = default!;

    [SubscribeLocalEvent]
    private void OnChangeMoodTrigger(EntityUid uid, ChangeMoodOnTriggerComponent component, TriggerEvent ev)
    {
        if (ev.Key == null || !component.KeysIn.Contains(ev.Key))
            return;

        if (component.Effect != null)
            _social.AddMoodEffect(uid, component.Effect.Value);

        if (component.RemovedEffect != null)
            _social.RemoveMoodEffect(uid, component.RemovedEffect.Value);

        ev.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnChangeOpinionTrigger(Entity<ChangeOpinionOnTriggerComponent> ent, ref TriggerEvent ev)
    {
        if (ev.User == null || ev.Key == null || !ent.Comp.KeysIn.Contains(ev.Key))
            return;

        if (ent.Comp.Effect != null)
        {
            if (ent.Comp.BothSide)
                _social.AddBothOpinionEffect(ent.Owner, ev.User.Value, ent.Comp.Effect.Value);
            else
                _social.AddOpinionEffect(ent.Owner, ev.User.Value, ent.Comp.Effect.Value);

            ev.Handled = true;
        }

        if (ent.Comp.RemovedEffect != null)
        {
            if (ent.Comp.BothSide)
                _social.RemoveBothOpinionEffect(ent.Owner, ev.User.Value, ent.Comp.RemovedEffect.Value);
            else
                _social.RemoveOpinionEffect(ent.Owner, ev.User.Value, ent.Comp.RemovedEffect.Value);

            ev.Handled = true;
        }
    }

    [SubscribeLocalEvent]
    private void OnHasOpinionEffectAttemptTrigger(Entity<HasOpinionEffectConditionComponent> ent, ref AttemptTriggerEvent args)
    {
        if (args.Key == null || !ent.Comp.Keys.Contains(args.Key))
            return;

        if (args.User == null)
        {
            args.Cancelled = true;
            return;
        }

        var has = _social.HasOpinionEffect(ent.Owner, args.User.Value, ent.Comp.Effect);
        args.Cancelled |= !ent.Comp.Invert && !has || ent.Comp.Invert && has;
    }

    [SubscribeLocalEvent]
    private void OnMoodAttemptTrigger(Entity<MoodTriggerConditionComponent> ent, ref AttemptTriggerEvent args)
    {
        if (args.Key == null || !ent.Comp.Keys.Contains(args.Key))
            return;

        var mood = _social.GetMood(ent.Owner);

        args.Cancelled |= (ent.Comp.Min == null || ent.Comp.Min > mood)
                          && (ent.Comp.Max == null || ent.Comp.Max < mood);
    }

    [SubscribeLocalEvent]
    private void OnOpinionAttemptTrigger(Entity<OpinionTriggerConditionComponent> ent, ref AttemptTriggerEvent args)
    {
        if (args.Key == null || !ent.Comp.Keys.Contains(args.Key))
            return;

        if (args.User == null)
        {
            args.Cancelled = true;
            return;
        }

        var opinion = _social.GetOpinion(ent.Owner, args.User.Value);

        if ((ent.Comp.Min == null || ent.Comp.Min > opinion)
            && (ent.Comp.Max == null || ent.Comp.Max < opinion))
            args.Cancelled = true;
    }
}
