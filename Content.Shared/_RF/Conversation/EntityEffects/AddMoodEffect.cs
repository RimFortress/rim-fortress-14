using Content.Shared._RF.Social;
using Content.Shared._RF.Social.Systems;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Conversation.EntityEffects;

/// <summary>
/// Adds an effect on the entity's mood
/// </summary>
public sealed partial class AddMoodEffect : EntityEffectBase<AddMoodEffect>
{
    /// <summary>
    /// Prototype of the effect
    /// </summary>
    [DataField]
    public ProtoId<SocialEffectPrototype> Proto;
}
