using JetBrains.Annotations;

namespace Content.Shared._RF.NPC.Pathfinding;

/// <summary>
/// The state of a navigation map region.
/// </summary>
[Flags]
public enum RfPathfindingFlags : byte
{
    None = 0,

    /// <summary>
    /// The poly has been replaced after a rebuild of its chunk and is no longer valid.
    /// </summary>
    Invalid = 1 << 0,

    /// <summary>
    /// The node is in open space.
    /// </summary>
    Space = 1 << 1,
}

/// <summary>
/// The data of a point or a poly of the navigation map that is relevant for pathfinding.
/// </summary>
/// <remarks>
/// Doors, access and climbing are not flags: they are path tags stored in <see cref="Facts"/>.
/// </remarks>
/// <param name="flags">The state of the region.</param>
/// <param name="layer">The collision layers of the entities that occupy the node.</param>
/// <param name="mask">The collision masks of the entities that occupy the node.</param>
/// <param name="facts">Everything that affects the cost of passing through the node.</param>
public struct RfPathfindingData(RfPathfindingFlags flags, int layer, int mask, PathNodeFacts facts)
    : IEquatable<RfPathfindingData>
{
    /// <summary>
    /// The data of a point that has not been calculated yet.
    /// </summary>
    public static readonly RfPathfindingData Invalid = new(RfPathfindingFlags.None, -1, -1, default);

    /// <summary>
    /// The state of the region.
    /// </summary>
    public RfPathfindingFlags Flags = flags;

    /// <summary>
    /// The collision layers of the entities that occupy the node.
    /// </summary>
    public int CollisionLayer = layer;

    /// <summary>
    /// The collision masks of the entities that occupy the node.
    /// </summary>
    public int CollisionMask = mask;

    /// <summary>
    /// Everything that affects the cost of passing through the node.
    /// </summary>
    public PathNodeFacts Facts = facts;

    /// <summary>
    /// Nothing is located on the node and nothing influences it, so the cost of passing through it is minimal.
    /// </summary>
    public readonly bool IsFreeSpace
        => Flags == RfPathfindingFlags.None && Facts.InfluenceCount == 0 && Facts.Destroy == null;

    /// <summary>
    /// Do the nodes belong to the same navigation region. The facts only affect the cost of passing through
    /// a node and not the shape of the region, so they are not compared.
    /// </summary>
    [PublicAPI, Pure]
    public readonly bool IsEquivalent(in RfPathfindingData other)
        => CollisionLayer == other.CollisionLayer && CollisionMask == other.CollisionMask && Flags == other.Flags;

    /// <summary>
    /// Are the data the same, including the facts.
    /// </summary>
    [PublicAPI, Pure]
    public readonly bool Equals(RfPathfindingData other)
        => IsEquivalent(other) && Facts.Equals(other.Facts);

    public override readonly bool Equals(object? obj) => obj is RfPathfindingData other && Equals(other);

    /// <summary>
    /// The hash is calculated only by the fields of <see cref="IsEquivalent"/>, which is enough for equal
    /// data to have equal hashes and does not require hashing the facts, which are expensive to hash.
    /// </summary>
    public override readonly int GetHashCode() => HashCode.Combine((int) Flags, CollisionLayer, CollisionMask);

    public static bool operator ==(RfPathfindingData left, RfPathfindingData right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(RfPathfindingData left, RfPathfindingData right)
    {
        return !(left == right);
    }
}
