using Content.Shared._RF.Conversation.Components;
using Content.Shared._RF.Social;
using Content.Shared._RF.Social.Systems;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Conversation.EntityEffects;

/// <summary>
/// Adds an effect on the entity's mood.
/// </summary>
public sealed partial class AddMood : EntityEffectBase<AddMood>
{
    /// <summary>
    /// Prototype of the effect.
    /// </summary>
    [DataField]
    public ProtoId<SocialEffectPrototype> Proto;
}

public sealed class AddMoodEntityEffectsSystem : EntityEffectSystem<ConversationActorComponent, AddMood>
{
    [Dependency] private SocialSystem _social = default!;

    protected override void Effect(Entity<ConversationActorComponent> ent, ref EntityEffectEvent<AddMood> args)
    {
        _social.AddMoodEffect(ent.Owner, args.Effect.Proto);
    }
}
