using Content.Shared._RF.NPC.Pathfinding.Systems;

namespace Content.Shared._RF.NPC.Pathfinding.Conditions;

/// <summary>
/// At least one of the conditions is met.
/// </summary>
public sealed partial class Or : BasePathCondition<Or>
{
    [DataField(required: true)]
    public List<PathCondition> Conditions = new();
}

public sealed class OrPathConditionSystem : PathConditionSystem<Or>
{
    protected override bool ConditionCheck(PathNodeContext ctx, Or condition)
    {
        foreach (var inner in condition.Conditions)
        {
            if (Pathfinding.CheckCondition(ctx, inner))
                return true;
        }

        return false;
    }
}
