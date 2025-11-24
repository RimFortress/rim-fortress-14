using Content.Shared._RF.Socialization;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

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

    /// <summary>
    /// Tags to check for the opinion of one entity to another
    /// </summary>
    [DataField]
    public List<ProtoId<TagPrototype>> Tags = new();

    /// <summary>
    /// Should we check for all tags or is it enough to check for any one tag
    /// </summary>
    [DataField]
    public bool RequireAll = true;

    public override bool Check(EntityUid author, EntityUid? actor, EntityManager entMan)
    {
        if (actor == null)
            return false;

        var sys = entMan.System<SocializationSystem>();
        var opinion = sys.GetOpinion(author, actor.Value);
        var hasTags = false;

        foreach (var tag in Tags)
        {
            if (sys.HasOpinionTag(author, actor.Value, tag))
            {
                hasTags = true;

                if (!RequireAll)
                    break;
            }
            else if (RequireAll)
                return false;
        }

        return hasTags
               && (MoreThan == null || opinion > MoreThan)
               && (LessThan == null || opinion < LessThan);
    }
}
