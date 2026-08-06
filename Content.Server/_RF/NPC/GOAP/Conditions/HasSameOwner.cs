using Content.Server._RF.NPC.GOAP.Systems;
using Content.Shared._RF.NPC;
using Content.Shared._RF.NPC.GOAP;

namespace Content.Server._RF.NPC.GOAP.Conditions;

/// <summary>
/// Checks whether the target entity shares the same owner as the agent.
/// </summary>
public sealed partial class HasSameOwner : BaseGoapCondition<HasSameOwner>
{
    /// <summary>
    /// Target entity.
    /// </summary>
    [DataField(required: true)]
    public StateKey<EntityUid> TargetKey;
}

public sealed class HasSameOwnerConditionSystem : GoapConditionSystem<HasSameOwner>
{
    [Dependency] private readonly OwnershipSystem _ownership = default!;

    protected override bool ConditionCheck(EntityUid uid, GoapState state, HasSameOwner condition)
        => TryGetValue(state, condition, condition.TargetKey, out var target)
           && _ownership.HasSameOwner(target, uid);
}
