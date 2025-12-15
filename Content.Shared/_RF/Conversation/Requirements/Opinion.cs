using Content.Shared._RF.Social;
using Content.Shared._RF.Social.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

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
    /// Opinion effects to an entity that needs to be checked for
    /// </summary>
    [DataField]
    public List<ProtoId<SocialEffectPrototype>> HasEffects = new();

    public override bool Check(EntityUid author, EntityUid? actor, EntityManager entMan)
    {
        DebugTools.AssertNotNull(actor, "Opinion requirement can only be used as a requirement from one actor to another");

        if (actor == null)
            return false;

        var sys = entMan.System<SocialSystem>();

        foreach (var effect in HasEffects)
        {
            if (!sys.HasOpinionEffect(author, actor.Value, effect))
                return false;
        }

        var opinion = sys.GetOpinion(author, actor.Value);

        return (MoreThan == null || opinion > MoreThan) && (LessThan == null || opinion < LessThan);
    }
}
