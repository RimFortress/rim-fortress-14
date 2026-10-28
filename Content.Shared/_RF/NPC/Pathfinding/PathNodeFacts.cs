using System.Runtime.CompilerServices;
using Content.Shared._RF.NPC.Pathfinding.Systems;
using Content.Shared.Damage;

namespace Content.Shared._RF.NPC.Pathfinding;

/// <summary>
/// How close a node is to the nearest source of a path tag.
/// </summary>
/// <param name="Tag">The index of the tag in the tag cache of <see cref="Systems.RfPathfindingSystem"/>.</param>
/// <param name="Distance">
/// The distance to the nearest source in steps of <see cref="Systems.RfPathfindingSystem.DistanceStep"/>,
/// 0 if the source is on the node itself.
/// </param>
public readonly record struct PathInfluence(byte Tag, byte Distance);

[InlineArray(RfPathfindingSystem.InfluenceSlots)]
public struct PathInfluenceSlots
{
    private PathInfluence _element0;
}

/// <summary>
/// Facts about a path node that are baked into the navigation map. Costs are not stored here,
/// they are calculated from these facts by the path profile when the path is searched.
/// </summary>
/// <remarks>
/// Every tag is stored the same way: as a distance to its nearest source. A tag located on the node is a tag
/// at distance 0, and the strength of a tag's influence is calculated from the distance by the tag's curve.
/// </remarks>
public struct PathNodeFacts : IEquatable<PathNodeFacts>
{
    /// <summary>
    /// The number of used <see cref="Influence"/> slots.
    /// </summary>
    public byte InfluenceCount;

    /// <summary>
    /// Tags that influence the node, sorted by the tag index, so equal nodes have equal facts.
    /// </summary>
    public PathInfluenceSlots Influence;

    /// <summary>
    /// Damage that is needed to destroy the node, per damage type, null if the node cannot be destroyed.
    /// Every dealt type contributes proportionally: a node needing 200 Structural is destroyed by 100 Structural
    /// and 50% of the rest, a node that does not list a type ignores it entirely.
    /// </summary>
    public DamageSpecifier? Destroy;

    public bool Equals(PathNodeFacts other)
    {
        if (InfluenceCount != other.InfluenceCount || !Equals(Destroy, other.Destroy))
            return false;

        for (var i = 0; i < InfluenceCount; i++)
        {
            if (Influence[i] != other.Influence[i])
                return false;
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is PathNodeFacts other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(InfluenceCount);

        for (var i = 0; i < InfluenceCount; i++)
        {
            hash.Add(Influence[i]);
        }

        if (Destroy == null)
            return hash.ToHashCode();

        // DamageSpecifier hashes by reference, so the equal values have to be hashed manually.
        var destroy = 0;

        foreach (var (type, amount) in Destroy.DamageDict)
        {
            destroy ^= HashCode.Combine(type, amount);
        }

        hash.Add(destroy);

        return hash.ToHashCode();
    }

    public static bool operator ==(PathNodeFacts left, PathNodeFacts right) => left.Equals(right);

    public static bool operator !=(PathNodeFacts left, PathNodeFacts right) => !(left == right);
}
