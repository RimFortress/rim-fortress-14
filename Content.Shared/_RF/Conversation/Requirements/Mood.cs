using Content.Shared._RF.Social.Systems;

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

    public override bool Check(EntityUid author, EntityUid? actor, EntityManager entMan)
    {
        var mood = entMan.System<SocialSystem>().GetMood(author);
        return (MoreThan == null || mood > MoreThan)
               && (LessThan == null || mood < LessThan);
    }
}
