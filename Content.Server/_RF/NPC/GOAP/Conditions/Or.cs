using Content.Server._RF.NPC.GOAP.Systems;
using Content.Shared._RF.NPC.GOAP;

namespace Content.Server._RF.NPC.GOAP.Conditions;

public sealed partial class Or : BaseGoapCondition<Or>
{
    [DataField(required: true)]
    public List<GoapCondition> Conditions = new();
}

public sealed class OrConditionSystem : GoapConditionSystem<Or>
{
    protected override bool ConditionCheck(EntityUid uid, GoapState state, Or condition)
    {
        foreach (var con in condition.Conditions)
        {
            if (!con.Check(uid, state, Goap, out var dump))
                continue;

            CreateDump(state, condition, dump?.Dump);
            return true;
        }

        return false;
    }
}
