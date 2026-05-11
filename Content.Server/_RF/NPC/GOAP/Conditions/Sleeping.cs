using Content.Server._RF.NPC.GOAP.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared.Bed.Sleep;

namespace Content.Server._RF.NPC.GOAP.Conditions;

/// <summary>
/// Checks whether the agent is sleeping.
/// </summary>
public sealed partial class Sleeping : BaseGoapCondition<Sleeping>;

public sealed class SleepingConditionSystem : GoapConditionSystem<Sleeping>
{
    [Dependency] private readonly EntityQuery<SleepingComponent> _query = default!;

    protected override bool ConditionCheck(EntityUid uid, GoapState state, Sleeping condition)
        => _query.HasComp(uid);
}
