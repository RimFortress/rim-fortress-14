using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared._RF.Social;
using Content.Shared._RF.Social.Components;
using Content.Shared._RF.Social.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.NPC.GOAP.Conditions.Social;

/// <summary>
/// Checks the mood level of the entity.
/// </summary>
public sealed partial class Mood : BaseGoapCondition<Mood>
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

public sealed class MoodGoapConditionSystem : GoapConditionSystem<Mood>
{
    [Dependency] private readonly SocialSystem _social = default!;
    [Dependency] private readonly EntityQuery<SocialComponent> _query = default!;

    protected override bool ConditionCheck(EntityUid uid, GoapState state, Mood condition)
    {
        if (!_query.TryComp(uid, out var comp))
            return false;

        var ent = new Entity<SocialComponent?>(uid, comp);
        var mood = _social.GetMood(ent);

        foreach (var effect in condition.HasEffects)
        {
            if (!_social.HasMoodEffect(ent, effect))
                return false;
        }

        return (condition.MoreThan == null || mood > condition.MoreThan)
               && (condition.LessThan == null || mood < condition.LessThan);
    }
}
