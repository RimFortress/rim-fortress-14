namespace Content.Shared._RF.NPC.Pathfinding.Algorithms;

/// <summary>
/// Searches for the cheapest path to the target.
/// </summary>
public sealed partial class AStar : BasePathAlgorithm<AStar>
{
    /// <summary>
    /// How much the heuristic is trusted: 1 finds the cheapest path, higher values find a path faster,
    /// but not necessarily the cheapest one.
    /// </summary>
    [DataField]
    public float HeuristicWeight = 1f;

    /// <summary>
    /// The maximum number of nodes the search is allowed to expand, 0 for no limit.
    /// </summary>
    [DataField]
    public int MaxNodes;

    /// <summary>
    /// If true, and the target cannot be reached, the path to the closest reachable node is returned.
    /// </summary>
    [DataField]
    public bool AllowPartial = true;
}
