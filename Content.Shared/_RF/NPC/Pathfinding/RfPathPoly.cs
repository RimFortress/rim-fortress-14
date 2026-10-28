using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.Shared._RF.NPC.Pathfinding;

/// <summary>
/// A region of a tile of the navigation map in which the agent can move freely, a node of the path graph.
/// </summary>
/// <remarks>
/// A poly is a class because it is a node of a graph with references to its neighbors. When the chunk is rebuilt,
/// its polys are replaced and marked <see cref="RfPathfindingFlags.Invalid"/>, so anything that holds a poly
/// for a long time (a path search job) must check <see cref="IsValid"/> or hold a <see cref="Ref"/> instead.
/// </remarks>
public sealed class RfPathPoly(
    EntityUid graphUid,
    Vector2i chunkOrigin,
    byte tileIndex,
    Box2 box,
    RfPathfindingData data)
    : IEquatable<RfPathPoly>
{
    // The identity of the poly never changes, so its hash is calculated once.
    private readonly int _hash = HashCode.Combine(graphUid, chunkOrigin, tileIndex, box);

    /// <summary>
    /// The grid that owns the poly.
    /// </summary>
    [ViewVariables]
    public readonly EntityUid GraphUid = graphUid;

    /// <summary>
    /// The origin of the chunk of the grid that contains the poly.
    /// </summary>
    [ViewVariables]
    public readonly Vector2i ChunkOrigin = chunkOrigin;

    /// <summary>
    /// The index of the poly's tile in the chunk, see <see cref="RfPathfindingChunk.TileIndex"/>.
    /// </summary>
    [ViewVariables]
    public readonly byte TileIndex = tileIndex;

    /// <summary>
    /// The region of the grid that the poly covers.
    /// </summary>
    [ViewVariables]
    public readonly Box2 Box = box;

    /// <summary>
    /// The data of the poly.
    /// </summary>
    [ViewVariables]
    public RfPathfindingData Data = data;

    /// <summary>
    /// Polys that can be reached from this one, including those of other chunks.
    /// </summary>
    [ViewVariables]
    public readonly List<RfPathPoly> Neighbors = new(4);

    /// <summary>
    /// The center of the poly.
    /// </summary>
    public EntityCoordinates Coordinates => new(GraphUid, Box.Center);

    /// <summary>
    /// A reference to the poly that can be stored while the chunk is rebuilt.
    /// </summary>
    public PathNodeRef Ref => new(GraphUid, ChunkOrigin, TileIndex, Box);

    /// <summary>
    /// Has the poly not been replaced after a rebuild of its chunk.
    /// </summary>
    public bool IsValid() => (Data.Flags & RfPathfindingFlags.Invalid) == 0;

    /// <summary>
    /// Makes the poly a neighbor of this one, if it is not already.
    /// </summary>
    public void AddNeighbor(RfPathPoly poly)
    {
        if (!Neighbors.Contains(poly))
            Neighbors.Add(poly);
    }

    /// <summary>
    /// Is the poly the same region of the map as the other one, even if their facts differ.
    /// Neighbors are not compared.
    /// </summary>
    public bool IsEquivalent(RfPathPoly other)
    {
        return GraphUid == other.GraphUid &&
               ChunkOrigin == other.ChunkOrigin &&
               TileIndex == other.TileIndex &&
               Box.Equals(other.Box) &&
               Data.IsEquivalent(other.Data);
    }

    /// <summary>
    /// Is the poly the same as the other one, including its facts. Neighbors are not compared.
    /// </summary>
    public bool Equals(RfPathPoly? other)
    {
        if (ReferenceEquals(this, other))
            return true;

        return other != null &&
               _hash == other._hash &&
               GraphUid == other.GraphUid &&
               ChunkOrigin == other.ChunkOrigin &&
               TileIndex == other.TileIndex &&
               Box.Equals(other.Box) &&
               Data.Equals(other.Data);
    }

    public override bool Equals(object? obj) => obj is RfPathPoly other && Equals(other);

    public override int GetHashCode() => _hash;
}
