using System.Numerics;
using Robust.Shared.Serialization;

namespace Content.Shared._RF.NPC.Pathfinding;

/// <summary>
/// What the debug overlays of the pathfinding show, each is an overlay of its own.
/// </summary>
[Flags, Serializable, NetSerializable]
public enum PathDebugLayers : byte
{
    None = 0,

    /// <summary>
    /// The navigation map: its polys, and what is in them (a body, something that can be broken, no ground).
    /// </summary>
    Mesh = 1 << 0,

    /// <summary>
    /// What a node costs the agent: by the rules of a profile for a query.
    /// </summary>
    Cost = 1 << 1,

    /// <summary>
    /// The influence of a tag on the nodes.
    /// </summary>
    Influence = 1 << 2,

    /// <summary>
    /// The paths that the agents follow and the nodes that their searches have looked at.
    /// </summary>
    Routes = 1 << 3,
}

/// <summary>
/// What the poly is, for the mesh overlay.
/// </summary>
[Flags]
public enum PathDebugPolyKind : byte
{
    None = 0,

    /// <summary>
    /// There is a body with a hard fixture.
    /// </summary>
    Solid = 1 << 0,

    /// <summary>
    /// What is there can be destroyed.
    /// </summary>
    Destroyable = 1 << 1,

    /// <summary>
    /// There is no ground.
    /// </summary>
    Space = 1 << 2,

    /// <summary>
    /// The poly has been replaced by another one: a search that has it in hand finds it out.
    /// </summary>
    Invalid = 1 << 3,

    /// <summary>
    /// Some tag has influence on it.
    /// </summary>
    Influenced = 1 << 4,

    /// <summary>
    /// For the cost overlay: the node is blocked for the agent by collision.
    /// </summary>
    Blocked = 1 << 5,
}

/// <summary>
/// Asks the server for the data of the debug overlays of the pathfinding. Only for those who may debug. It is asked for again and
/// again while the overlays are on, what the server says is only for that moment.
/// </summary>
/// <param name="Layers">What is asked for.</param>
/// <param name="Area">In the world, the data is only for the polys that are in it.</param>
/// <param name="Profile">The profile of the costs, the first one of the agent if it is not said.</param>
/// <param name="Query">The query of the costs and of the routes, all of them for the routes if it is not said.</param>
/// <param name="Tag">The tag of the influence.</param>
/// <param name="Agent">Whose costs they are, the closest to the middle of the area if it is not said.</param>
[Serializable, NetSerializable]
public sealed class PathDebugRequest(
    PathDebugLayers layers,
    Box2 area,
    string? profile,
    string? query,
    string? tag,
    NetEntity? agent)
    : EntityEventArgs
{
    public readonly PathDebugLayers Layers = layers;
    public readonly Box2 Area = area;
    public readonly string? Profile = profile;
    public readonly string? Query = query;
    public readonly string? Tag = tag;
    public readonly NetEntity? Agent = agent;
}

/// <summary>
/// The data of the debug overlays of the pathfinding for a request.
/// </summary>
[Serializable, NetSerializable]
public sealed class PathDebugResponse(
    PathDebugLayers layers,
    string info,
    List<PathDebugGrid> mesh,
    List<PathDebugGrid> cost,
    List<PathDebugGrid> influence,
    List<PathDebugRoute> routes)
    : EntityEventArgs
{
    public readonly PathDebugLayers Layers = layers;

    /// <summary>
    /// What the data is of, in words: whose costs they are, by what profile and query, how many polys.
    /// </summary>
    public readonly string Info = info;

    public readonly List<PathDebugGrid> Mesh = mesh;

    public readonly List<PathDebugGrid> Cost = cost;

    public readonly List<PathDebugGrid> Influence = influence;

    public readonly List<PathDebugRoute> Routes = routes;
}

/// <summary>
/// The polys of a grid. The boxes are in the coordinates of the grid, so they move with it.
/// </summary>
[Serializable, NetSerializable]
public sealed record PathDebugGrid(NetEntity Grid, List<PathDebugPoly> Polys);

/// <param name="Box">Where the poly is, in the coordinates of the grid.</param>
/// <param name="Kind">What it is, see <see cref="PathDebugPolyKind"/>.</param>
/// <param name="Value">The cost of the node for the cost overlay, the influence for the influence one.</param>
[Serializable, NetSerializable]
public readonly record struct PathDebugPoly(Box2 Box, byte Kind, float Value);

/// <summary>
/// A path that an agent follows, and what its last search looked at.
/// </summary>
/// <param name="Agent">The agent.</param>
/// <param name="Name">What it is called.</param>
/// <param name="Graph">The grid, the points are in its coordinates.</param>
/// <param name="Query">The query of the order.</param>
/// <param name="Profile">The profile that gave the path.</param>
/// <param name="Status">The status of the steering.</param>
/// <param name="Phase">What the agent does to carry out the order.</param>
/// <param name="Index">The node it is at.</param>
/// <param name="Repaths">How many times the path has been searched for again.</param>
/// <param name="Nodes">The nodes of the path.</param>
/// <param name="End">Where the path ends.</param>
/// <param name="Target">The target of the order.</param>
/// <param name="Goals">What the agent is to do on the way.</param>
/// <param name="Avoid">The places that the search keeps away from.</param>
/// <param name="Explored">The nodes that the last search looked at, by the same query.</param>
/// <param name="ExploredGraph">The grid of the explored nodes.</param>
[Serializable, NetSerializable]
public sealed record PathDebugRoute(
    NetEntity Agent,
    string Name,
    NetEntity Graph,
    string Query,
    string Profile,
    string Status,
    string Phase,
    int Index,
    int Repaths,
    List<Vector2> Nodes,
    Vector2 End,
    Vector2? Target,
    List<PathDebugGoal> Goals,
    List<PathDebugAvoid> Avoid,
    List<PathDebugExplored> Explored,
    NetEntity ExploredGraph);

/// <param name="Position">The node of the goal.</param>
/// <param name="Goal">What is to be done there.</param>
/// <param name="Pending">It is the goal that the agent is going to now.</param>
[Serializable, NetSerializable]
public readonly record struct PathDebugGoal(Vector2 Position, string Goal, bool Pending);

[Serializable, NetSerializable]
public readonly record struct PathDebugAvoid(Vector2 Position, float Radius);

/// <param name="Box">The poly.</param>
/// <param name="G">What it costs to get to.</param>
/// <param name="Closed">It was expanded, and not only reached.</param>
[Serializable, NetSerializable]
public readonly record struct PathDebugExplored(Box2 Box, float G, bool Closed);
