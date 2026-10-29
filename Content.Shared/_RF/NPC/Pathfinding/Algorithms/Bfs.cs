namespace Content.Shared._RF.NPC.Pathfinding.Algorithms;

/// <summary>
/// How <see cref="Bfs"/> chooses the node the path leads to among the nodes it has reached.
/// </summary>
public enum BfsPick : byte
{
    /// <summary>
    /// A random node.
    /// </summary>
    Random,

    /// <summary>
    /// The node that is the farthest from the start.
    /// </summary>
    Farthest,
}

/// <summary>
/// Searches for the nodes reachable from the start without a destination, for example, to pick a place to
/// wander to or to run to. The path leads to a node chosen by the algorithm.
/// </summary>
public sealed partial class Bfs : BasePathAlgorithm<Bfs>
{
    /// <summary>
    /// How far from the start the search is allowed to go. The range of the request replaces it, if it has one.
    /// </summary>
    [DataField]
    public float Range = 20f;

    /// <summary>
    /// Nodes that are closer to the start than this are not chosen.
    /// </summary>
    [DataField]
    public float MinRange;

    /// <summary>
    /// How the node the path leads to is chosen.
    /// </summary>
    [DataField]
    public BfsPick Pick = BfsPick.Random;

    /// <summary>
    /// The maximum number of nodes the search is allowed to expand, 0 for no limit.
    /// </summary>
    [DataField]
    public int MaxNodes = 400;

    /// <summary>
    /// Paths that cost more than this are not considered. Nodes that are expensive to pass through, such as walls,
    /// cannot be passed through at all if the limit is lower than their cost. Null for no limit.
    /// </summary>
    [DataField]
    public float? MaxCost;

    /// <summary>
    /// The maximum number of chunks of the navigation map the search is allowed to build, 0 for no limit.
    /// The map is built as the search reaches the places that are not on it yet, chunks that are already on the
    /// map are free.
    /// </summary>
    [DataField]
    public int MaxChunks = 8;
}
