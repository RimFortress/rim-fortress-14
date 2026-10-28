namespace Content.Shared._RF.NPC.Pathfinding.Algorithms;

/// <summary>
/// Searches for the closest reachable nodes without a destination, for example, to pick a place to run to.
/// The path leads to a node chosen by the algorithm.
/// </summary>
public sealed partial class Bfs : BasePathAlgorithm<Bfs>
{
    /// <summary>
    /// How far from the start the search is allowed to go.
    /// </summary>
    [DataField]
    public float Range = 20f;

    /// <summary>
    /// The maximum number of nodes the search is allowed to expand.
    /// </summary>
    [DataField]
    public int MaxNodes = 400;
}
