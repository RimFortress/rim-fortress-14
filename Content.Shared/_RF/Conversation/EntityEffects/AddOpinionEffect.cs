using Content.Shared._RF.Social;
using Content.Shared._RF.Social.Systems;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Conversation.EntityEffects;

/// <summary>
/// Adds an effect on the opinion of an entity to any of the participants in the conversation
/// </summary>
public sealed partial class AddOpinionEffect : EntityEffectBase<AddOpinionEffect>
{
    /// <summary>
    /// IDs of conversation participants, effect on opinion to which should be added
    /// </summary>
    [DataField]
    public List<string> Actors = new();

    /// <summary>
    /// Prototype of the effect
    /// </summary>
    [DataField]
    public ProtoId<SocialEffectPrototype> Proto;
}
