using Content.Shared._RF.Conversation.Systems;
using Content.Shared._RF.Social;
using Content.Shared._RF.Social.Components;
using Content.Shared._RF.Social.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._RF.Conversation.Requirements;

/// <summary>
/// Check for one entity's opinion of another.
/// </summary>
public sealed partial class Opinion : BaseConversationCondition<Opinion>
{
    /// <summary>
    /// Minimum opinion level.
    /// </summary>
    [DataField]
    public int? MoreThan;

    /// <summary>
    /// Maximum opinion level.
    /// </summary>
    [DataField]
    public int? LessThan;

    /// <summary>
    /// Opinion effects to an entity that needs to be checked for.
    /// </summary>
    [DataField]
    public List<ProtoId<SocialEffectPrototype>> HasEffects = new();
}

public sealed class OpinionConversationConditionSystem : ConversationConditionSystem<SocialComponent, Opinion>
{
    [Dependency] private readonly SocialSystem _social = default!;

    protected override bool Check(Entity<SocialComponent> ent, EntityUid? other, Opinion condition)
    {
        if (other == null)
        {
            DebugTools.Assert(false, "Opinion requirement can only be used as a requirement from one actor to another");
            return false;
        }

        foreach (var effect in condition.HasEffects)
        {
            if (!_social.HasOpinionEffect(ent.AsNullable(), other.Value, effect))
                return false;
        }

        var opinion = _social.GetOpinion(ent.AsNullable(), other.Value);

        return (condition.MoreThan == null || opinion > condition.MoreThan)
               && (condition.LessThan == null || opinion < condition.LessThan);
    }
}
