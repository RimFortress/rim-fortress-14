using Content.Shared._RF.Social.Systems;

namespace Content.Shared._RF.Conversation.Requirements;

/// <summary>
/// Check for one entity's opinion of another
/// </summary>
public sealed partial class Opinion : ConversationActorRequirement
{
    /// <summary>
    /// Minimum opinion level
    /// </summary>
    [DataField]
    public int? MoreThan;

    /// <summary>
    /// Maximum opinion level
    /// </summary>
    [DataField]
    public int? LessThan;

    public override bool Check(EntityUid author, EntityUid? actor, EntityManager entMan)
    {
        if (actor == null)
            return false;

        var sys = entMan.System<SocialSystem>();
        var opinion = sys.GetOpinion(author, actor.Value);

        return (MoreThan == null || opinion > MoreThan) && (LessThan == null || opinion < LessThan);
    }
}
