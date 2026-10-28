namespace Content.Shared._RF.NPC.Pathfinding.Systems;

/// <summary>
/// An entity system that implements path condition check.
/// </summary>
/// <typeparam name="T">Path condition type.</typeparam>
public abstract partial class PathConditionSystem<T> : EntitySystem where T : BasePathCondition<T>
{
    [Dependency] protected RfPathfindingSystem Pathfinding = null!;
    
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PathConditionCheck<T>>(OnConditionCheck);
    }

    private void OnConditionCheck(ref PathConditionCheck<T> ev)
    {
        ev.Result = ConditionCheck(ev.Node, ev.Condition);
    }

    /// <summary>
    /// Checks whether the path node satisfies the condition.
    /// </summary>
    /// <remarks>
    /// The result must depend only on the node context and the condition itself:
    /// it is cached for all nodes with equal facts.
    /// </remarks>
    /// <param name="ctx">The node and the agent for which the path is being searched.</param>
    /// <param name="condition">Path condition.</param>
    /// <returns>True, if the check is passed; otherwise, false.</returns>
    protected abstract bool ConditionCheck(PathNodeContext ctx, T condition);
}
