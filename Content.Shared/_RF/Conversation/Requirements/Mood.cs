using Content.Shared._RF.Social;
using Content.Shared._RF.Social.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Conversation.Requirements;

/// <summary>
/// Checks the mood level of the entity
/// </summary>
public sealed partial class Mood : ConversationActorRequirement
{
    /// <summary>
    /// Minimum mood level
    /// </summary>
    [DataField]
    public int? MoreThan;

    /// <summary>
    /// Maximum mood level
    /// </summary>
    [DataField]
    public int? LessThan;

    /// <summary>
    /// Mood effects that need to be checked for
    /// </summary>
    [DataField]
    public List<ProtoId<SocialEffectPrototype>> HasEffects = new();

    public override bool Check(EntityUid author, EntityUid? actor, EntityManager entMan)
    {
        var sys = entMan.System<SocialSystem>();
        var mood = sys.GetMood(author);

        foreach (var effect in HasEffects)
        {
            if (!sys.HasMoodEffect(author, effect))
                return false;
        }

        return (MoreThan == null || mood > MoreThan)
               && (LessThan == null || mood < LessThan);
    }
}
