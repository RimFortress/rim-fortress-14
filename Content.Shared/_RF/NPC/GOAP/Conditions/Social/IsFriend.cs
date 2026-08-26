using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared.NPC.Systems;

namespace Content.Shared._RF.NPC.GOAP.Conditions.Social;

/// <summary>
/// Checks whether the target entity is a friend.
/// </summary>
public sealed partial class IsFriend : BaseGoapCondition<IsFriend>
{
    /// <summary>
    /// Target enitity.
    /// </summary>
    [DataField(required: true)]
    public StateKey<EntityUid> TargetKey;
}

public sealed class IsFriendGoapCondition : GoapConditionSystem<IsFriend>
{
    [Dependency] private readonly NpcFactionSystem _faction = default!;

    protected override bool ConditionCheck(EntityUid uid, GoapState state, IsFriend condition)
        => TryGet(state, condition.TargetKey, out var target) && _faction.IsEntityFriendly(uid, target);
}
