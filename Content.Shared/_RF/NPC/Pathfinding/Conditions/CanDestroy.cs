using Content.Shared._RF.NPC.Pathfinding.Systems;

namespace Content.Shared._RF.NPC.Pathfinding.Conditions;

/// <summary>
/// The agent is able to destroy the node with the damage it deals.
/// </summary>
public sealed partial class CanDestroy : BasePathCondition<CanDestroy>
{
    /// <summary>
    /// The agent must be able to do this in greater than this number of seconds.
    /// </summary>
    [DataField]
    public float? MoreThan;

    /// <summary>
    /// The agent must be able to do this in less than this number of seconds.
    /// </summary>
    [DataField]
    public float? LessThan;

    /// <summary>
    /// The name of the variable to which the time to destroy in seconds will be written.
    /// </summary>
    [DataField]
    public string Var = "destroyTime";
}

public sealed partial class CanDestroyPathConditionSystem : PathConditionSystem<CanDestroy>
{
    protected override bool ConditionCheck(PathNodeContext ctx, CanDestroy condition)
    {
        if (ctx.Facts.Destroy is not { } required)
            return false;

        var time = Pathfinding.GetDestroyTime(required, ctx.Attacks);

        if (condition.MoreThan != null && time <= condition.MoreThan
            || condition.LessThan != null && time >= condition.LessThan)
            return false;

        ctx.Variables[condition.Var] = time;
        return true;
    }
}
