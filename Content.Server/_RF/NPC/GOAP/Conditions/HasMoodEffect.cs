using Content.Server._RF.NPC.GOAP.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.Social;
using Content.Shared._RF.Social.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.GOAP.Conditions;

/// <summary>
/// Checks whether the target entity has a mood effect.
/// </summary>
public sealed partial class HasMoodEffect : BaseGoapCondition<HasMoodEffect>
{
    /// <summary>
    /// Target entity.
    /// </summary>
    [DataField]
    public StateKey<EntityUid> TargetKey = GoapState.Owner;

    /// <summary>
    /// Mood effect prototype.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<SocialEffectPrototype> Effect;
}

public sealed class HasMoodEffectGoapConditionSystem : GoapConditionSystem<HasMoodEffect>
{
    [Dependency] private readonly SocialSystem _social = default!;

    protected override bool ConditionCheck(EntityUid uid, GoapState state, HasMoodEffect condition)
        => TryGetValue(state, condition, condition.TargetKey, out var target)
           && _social.HasMoodEffect(target, condition.Effect);
}
