using Content.Shared._RF.Conversation.Components;
using Content.Shared._RF.Conversation.EntityEffects;
using Content.Shared._RF.Needs.Systems;
using Content.Shared._RF.Social.Systems;
using Content.Shared.EntityEffects;

namespace Content.Shared.Conversation;

public sealed class AddMoodEntityEffectsSystem : EntityEffectSystem<ConversationActorComponent, AddMoodEffect>
{
    [Dependency] private readonly SocialSystem _social = default!;

    protected override void Effect(Entity<ConversationActorComponent> ent, ref EntityEffectEvent<AddMoodEffect> args)
    {
        _social.AddMoodEffect(ent.Owner, args.Effect.Proto);
    }
}

public sealed class AddOpinionEntityEffectsSystem : EntityEffectSystem<ConversationActorComponent, AddOpinionEffect>
{
    [Dependency] private readonly SocialSystem _social = default!;

    protected override void Effect(Entity<ConversationActorComponent> ent, ref EntityEffectEvent<AddOpinionEffect> args)
    {
        foreach (var actor in args.Effect.Actors)
        {
            if (ent.Comp.Actors.TryGetValue(actor, out var uid))
                _social.AddOpinionEffect(ent.Owner, uid, args.Effect.Proto);
        }
    }
}

public sealed class ChangeNeedEntityEffectsSystem : EntityEffectSystem<ConversationActorComponent, ChangeNeedEffect>
{
    [Dependency] private readonly NeedsSystem _needs = default!;

    protected override void Effect(Entity<ConversationActorComponent> ent, ref EntityEffectEvent<ChangeNeedEffect> args)
    {
        _needs.AddValue(ent.Owner, args.Effect.Need, args.Effect.Amount);
    }
}
