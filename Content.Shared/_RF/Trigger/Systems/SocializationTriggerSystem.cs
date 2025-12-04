using Content.Shared._RF.Socialization;
using Content.Shared._RF.Trigger.Components;
using Content.Shared.Trigger;

namespace Content.Shared._RF.Trigger.Systems;

public sealed class SocializationTriggerSystem : EntitySystem
{
    [Dependency] private readonly SocializationSystem _socialization  = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<ChangeMoodOnTriggerComponent, TriggerEvent>(OnChangeMoodTrigger);
        SubscribeLocalEvent<ChangeOpinionOnTriggerComponent, TriggerEvent>(OnChangeOpinionTrigger);

        SubscribeLocalEvent<HasOpinionEffectConditionComponent, AttemptTriggerEvent>(OnHasOpinionEffectAttemptTrigger);
        SubscribeLocalEvent<MoodTriggerConditionComponent, AttemptTriggerEvent>(OnMoodAttemptTrigger);
        SubscribeLocalEvent<OpinionTriggerConditionComponent, AttemptTriggerEvent>(OnOpinionAttemptTrigger);
    }

    private void OnChangeMoodTrigger(EntityUid uid, ChangeMoodOnTriggerComponent component, TriggerEvent ev)
    {
        if (ev.Key == null || !component.KeysIn.Contains(ev.Key))
            return;

        if (component.Effect != null)
            _socialization.AddMoodEffect(uid, component.Effect.Value);

        if (component.RemovedEffect != null)
            _socialization.RemoveMoodEffect(uid, component.RemovedEffect.Value);

        ev.Handled = true;
    }

    private void OnChangeOpinionTrigger(Entity<ChangeOpinionOnTriggerComponent> ent, ref TriggerEvent ev)
    {
        if (ev.User == null || ev.Key == null || !ent.Comp.KeysIn.Contains(ev.Key))
            return;

        if (ent.Comp.Effect != null)
        {
            if (ent.Comp.BothSide)
                _socialization.AddBothOpinionEffect(ent.Owner, ev.User.Value, ent.Comp.Effect.Value);
            else
                _socialization.AddOpinionEffect(ent.Owner, ev.User.Value, ent.Comp.Effect.Value);

            ev.Handled = true;
        }

        if (ent.Comp.RemovedEffect != null)
        {
            if (ent.Comp.BothSide)
                _socialization.RemoveBothOpinionEffect(ent.Owner, ev.User.Value, ent.Comp.RemovedEffect.Value);
            else
                _socialization.RemoveOpinionEffect(ent.Owner, ev.User.Value, ent.Comp.RemovedEffect.Value);

            ev.Handled = true;
        }
    }

    private void OnHasOpinionEffectAttemptTrigger(Entity<HasOpinionEffectConditionComponent> ent, ref AttemptTriggerEvent args)
    {
        if (args.Key == null || !ent.Comp.Keys.Contains(args.Key))
            return;

        if (args.User == null)
        {
            args.Cancelled = true;
            return;
        }

        var has = _socialization.HasOpinionEffect(ent.Owner, args.User.Value, ent.Comp.Effect);
        args.Cancelled |= !ent.Comp.Invert && !has || ent.Comp.Invert && has;
    }

    private void OnMoodAttemptTrigger(Entity<MoodTriggerConditionComponent> ent, ref AttemptTriggerEvent args)
    {
        if (args.Key == null || !ent.Comp.Keys.Contains(args.Key))
            return;

        foreach (var tag in ent.Comp.Tags)
        {
            if (_socialization.HasMoodTag(ent.Owner, tag))
                continue;

            args.Cancelled = true;
            return;
        }

        var mood = _socialization.GetMood(ent.Owner);

        args.Cancelled |= (ent.Comp.Min == null || ent.Comp.Min > mood)
                          && (ent.Comp.Max == null || ent.Comp.Max < mood);
    }

    private void OnOpinionAttemptTrigger(Entity<OpinionTriggerConditionComponent> ent, ref AttemptTriggerEvent args)
    {
        if (args.Key == null || !ent.Comp.Keys.Contains(args.Key))
            return;

        if (args.User == null)
        {
            args.Cancelled = true;
            return;
        }

        foreach (var tag in ent.Comp.Tags)
        {
            if (_socialization.HasOpinionTag(ent.Owner, args.User.Value, tag))
                continue;

            args.Cancelled = true;
            return;
        }

        var opinion = _socialization.GetOpinion(ent.Owner, args.User.Value);

        if ((ent.Comp.Min == null || ent.Comp.Min > opinion)
            && (ent.Comp.Max == null || ent.Comp.Max < opinion))
            args.Cancelled = true;
    }
}
