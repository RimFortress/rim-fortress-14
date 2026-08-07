using Content.Shared._RF.Conversation.Systems;
using Content.Shared._RF.Social;
using Content.Shared._RF.Social.Components;
using Content.Shared._RF.Social.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Conversation.Requirements;

/// <summary>
/// Checks the mood level of the entity.
/// </summary>
public sealed partial class Mood : BaseConversationCondition<Mood>
{
    /// <summary>
    /// Minimum mood level.
    /// </summary>
    [DataField]
    public int? MoreThan;

    /// <summary>
    /// Maximum mood level.
    /// </summary>
    [DataField]
    public int? LessThan;

    /// <summary>
    /// Mood effects that need to be checked for.
    /// </summary>
    [DataField]
    public List<ProtoId<SocialEffectPrototype>> HasEffects = new();
}

public sealed class MoodConversationConditionSystem : ConversationConditionSystem<SocialComponent, Mood>
{
    [Dependency] private readonly SocialSystem _social = default!;

    protected override bool Check(Entity<SocialComponent> ent, EntityUid? other, Mood condition)
    {
        var mood = _social.GetMood(ent.AsNullable());

        foreach (var effect in condition.HasEffects)
        {
            if (!_social.HasMoodEffect(ent.AsNullable(), effect))
                return false;
        }

        return (condition.MoreThan == null || mood > condition.MoreThan)
               && (condition.LessThan == null || mood < condition.LessThan);
    }
}
