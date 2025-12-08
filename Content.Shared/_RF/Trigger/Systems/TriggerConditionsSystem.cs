using Content.Shared._RF.Trigger.Components.Conditions;
using Content.Shared.Tag;
using Content.Shared.Trigger;

namespace Content.Shared._RF.Trigger.Systems;

public sealed class TriggerConditionsSystem : EntitySystem
{
    [Dependency] private readonly TagSystem _tag = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<UserTagsTriggerConditionComponent, AttemptTriggerEvent>(OnUserTagsAttemptTrigger);
    }

    private void OnUserTagsAttemptTrigger(Entity<UserTagsTriggerConditionComponent> ent, ref AttemptTriggerEvent args)
    {
        if (args.Key == null || !ent.Comp.Keys.Contains(args.Key))
            return;

        if (args.User == null)
        {
            args.Cancelled = true;
            return;
        }

        var has = ent.Comp.RequireAll
            ? _tag.HasAllTags(args.User.Value, ent.Comp.Tags)
            : _tag.HasAnyTag(args.User.Value, ent.Comp.Tags);

        if (!has && !ent.Comp.Invert || has && ent.Comp.Invert)
            args.Cancelled = true;
    }
}
