namespace Content.Shared._RF.NPC.Pathfinding;

/// <summary>
/// The state of the path request of an agent.
/// </summary>
public enum PathRequestStatus : byte
{
    /// <summary>
    /// The agent has no request.
    /// </summary>
    None,

    /// <summary>
    /// The path is being searched for.
    /// </summary>
    Searching,

    /// <summary>
    /// The search is over, the result is waiting to be taken.
    /// </summary>
    Finished,
}
