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
    public int MaxNodes = 4000;

    /// <summary>
    /// The maximum number of chunks of the navigation map the search is allowed to build, 0 for no limit.
    /// The map is built as the search reaches the places that are not on it yet, chunks that are already on the
    /// map are free.
    /// </summary>
    [DataField]
    public int MaxChunks = 32;

    /// <summary>
    /// The search is limited to an ellipse whose foci are the start and the target: a node is only expanded if
    /// the sum of its distances to them exceeds the distance between them by no more than twice this margin
    /// (in tiles). Null for no limit.
    /// </summary>
    [DataField]
    public float? AreaMargin = 32f;

    /// <summary>
    /// Paths that cost more than this are not considered. Nodes that are expensive to pass through, such as walls,
    /// cannot be passed through at all if the limit is lower than their cost. Null for no limit.
    /// </summary>
    [DataField]
    public float? MaxCost;

    /// <summary>
    /// If true, and the target cannot be reached, the path to the closest reachable node is returned.
    /// </summary>
    [DataField]
    public bool AllowPartial = true;
}
