using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared.Buckle;

namespace Content.Shared._RF.NPC.GOAP.Conditions.Interaction;

/// <summary>
/// True if the agent is buckled, false otherwise.
/// </summary>
public sealed partial class IsBuckled : BaseGoapCondition<IsBuckled>;

public sealed class IsBuckledConditionSystem : GoapConditionSystem<IsBuckled>
{
    [Dependency] private readonly SharedBuckleSystem _buckle = default!;

    protected override bool ConditionCheck(EntityUid uid, GoapState state, IsBuckled condition) => _buckle.IsBuckled(uid);
}
