using Content.Shared._RF.NPC.Pathfinding.Systems;

namespace Content.Shared._RF.NPC.Pathfinding.Conditions;

/// <summary>
/// The node is physically blocked for the agent. Inverted, it means that the node is free.
/// </summary>
public sealed partial class Blocked : BasePathCondition<Blocked>;

public sealed class BlockedPathConditionSystem : PathConditionSystem<Blocked>
{
    protected override bool ConditionCheck(PathNodeContext ctx, Blocked condition) => ctx.Blocked;
}
