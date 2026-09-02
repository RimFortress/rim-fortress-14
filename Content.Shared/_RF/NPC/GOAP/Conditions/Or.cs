using Content.Shared._RF.NPC.GOAP.Systems;

namespace Content.Shared._RF.NPC.GOAP.Conditions;

public sealed partial class Or : BaseGoapCondition<Or>
{
    [DataField(required: true)]
    public List<GoapCondition> Conditions = new();
}

public sealed partial class OrConditionSystem : GoapConditionSystem<Or>
{
    protected override bool ConditionCheck(EntityUid uid, GoapState state, Or condition)
    {
        foreach (var con in condition.Conditions)
        {
            if (!con.Check(uid, state, Goap, out var dump))
                continue;

            CreateDump(dump?.Dump ?? "");
            return true;
        }

        return false;
    }
}
