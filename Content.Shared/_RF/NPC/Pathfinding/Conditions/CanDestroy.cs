using Content.Shared._RF.NPC.Pathfinding.Systems;
using Content.Shared.FixedPoint;

namespace Content.Shared._RF.NPC.Pathfinding.Conditions;

/// <summary>
/// The agent is able to destroy the node with the damage it deals.
/// </summary>
public sealed partial class CanDestroy : BasePathCondition<CanDestroy>
{
    /// <summary>
    /// The agent must be able to do this in less than this number of seconds.
    /// </summary>
    [DataField]
    public float LessThan = float.PositiveInfinity;

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

        // Every type of damage brings the node closer to destruction proportionally to what it needs.
        var progress = 0f;

        foreach (var (type, needed) in required.DamageDict)
        {
            if (needed > FixedPoint2.Zero && ctx.Damage.DamageDict.TryGetValue(type, out var dealt) && dealt > FixedPoint2.Zero)
                progress += dealt.Float() / needed.Float();
        }

        if (progress <= 0f)
            return false;

        var time = 1f / progress;

        if (time >= condition.LessThan)
            return false;

        ctx.Variables[condition.Var] = time;
        return true;
    }
}
