using Content.Server._RF.NPC.GOAP.Systems;
using Content.Server.Buckle.Systems;
using Content.Shared._RF.NPC.GOAP;

namespace Content.Server._RF.NPC.GOAP.Conditions;

/// <summary>
/// True if the agent is buckled, false otherwise.
/// </summary>
public sealed partial class IsBuckled : BaseGoapCondition<IsBuckled>;

public sealed class IsBuckledConditionSystem : GoapConditionSystem<IsBuckled>
{
    [Dependency] private readonly BuckleSystem _buckle = default!;

    protected override bool ConditionCheck(EntityUid uid, GoapState state, IsBuckled condition)
        => _buckle.IsBuckled(uid);
}
