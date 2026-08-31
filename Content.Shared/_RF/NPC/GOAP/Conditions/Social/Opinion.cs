using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared._RF.Social;
using Content.Shared._RF.Social.Components;
using Content.Shared._RF.Social.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.NPC.GOAP.Conditions.Social;

/// <summary>
/// Check for one entity's opinion of the target.
/// </summary>
public sealed partial class Opinion : BaseGoapCondition<Opinion>
{
    /// <summary>
    /// Target entity.
    /// </summary>
    [DataField(required: true)]
    public StateKey<EntityUid> TargetKey;

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

public sealed class OpinionGoapConditionSystem : GoapConditionSystem<Opinion>
{
    [Dependency] private readonly SocialSystem _social = default!;
    [Dependency] private readonly EntityQuery<SocialComponent> _query = default!;

    protected override bool ConditionCheck(EntityUid uid, GoapState state, Opinion condition)
    {
        if (!_query.TryComp(uid, out var comp)
            || !TryGet(state, condition.TargetKey, out var target))
            return false;

        var ent = new Entity<SocialComponent?>(uid, comp);

        foreach (var effect in condition.HasEffects)
        {
            if (!_social.HasOpinionEffect(ent.AsNullable(), target, effect))
                return false;
        }

        var opinion = _social.GetOpinion(ent.AsNullable(), target);

        return (condition.MoreThan == null || opinion > condition.MoreThan)
               && (condition.LessThan == null || opinion < condition.LessThan);
    }
}
