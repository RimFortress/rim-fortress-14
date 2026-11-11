using Content.Server._RF.NPC.Pathfinding.Algorithms;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.Pathfinding.Components;
using Robust.Shared.Timing;

namespace Content.Server._RF.NPC.Pathfinding.Systems;

// What the debug overlays want to know about the searches.
public sealed partial class RfPathfindingSystem
{
    /// <summary>
    /// What a search looked at.
    /// </summary>
    /// <param name="At">When it was over.</param>
    /// <param name="Query">What it was for.</param>
    /// <param name="Profile">The profile it was with.</param>
    /// <param name="Graph">The grid.</param>
    /// <param name="Nodes">The polys that it reached, with what they cost to get to and whether they were expanded.</param>
    public sealed record ExploredSearch(
        TimeSpan At,
        string Query,
        string Profile,
        EntityUid Graph,
        List<(Box2 Box, float G, bool Closed)> Nodes);

    /// <summary>
    /// How many nodes of a search are kept.
    /// </summary>
    private const int MaxExploredKept = 6000;

    /// <summary>
    /// The searches are kept until this time, it is moved on while somebody looks at them.
    /// </summary>
    public TimeSpan DebugCaptureUntil;

    /// <summary>
    /// The last search of each agent, while somebody looks at them.
    /// </summary>
    public readonly Dictionary<EntityUid, ExploredSearch> Explored = new();

    public bool IsCapturing => _timing.CurTime < DebugCaptureUntil;

    /// <summary>
    /// Keeps the nodes that the search has looked at, if somebody is looking at them.
    /// </summary>
    public void CaptureExplored(Entity<PathProfilesComponent, GoapComponent> agent, PathSearchState state)
    {
        if (!IsCapturing || state.Nodes.Count == 0)
            return;

        var nodes = new List<(Box2, float, bool)>(Math.Min(state.Nodes.Count, MaxExploredKept));
        var graph = EntityUid.Invalid;

        foreach (var (poly, node) in state.Nodes)
        {
            if (nodes.Count >= MaxExploredKept)
                break;

            if (float.IsPositiveInfinity(node.G))
                continue;

            graph = poly.GraphUid;
            nodes.Add((poly.Box, node.G, node.Closed));
        }

        Explored[agent.Owner] = new ExploredSearch(
            _timing.CurTime,
            agent.Comp1.RequestQuery?.Id ?? "?",
            agent.Comp1.ActiveProfile?.Id ?? "?",
            graph,
            nodes);
    }
}
