using Robust.Shared.Serialization;

namespace Content.Shared.NPC;

[Serializable, NetSerializable]
public struct PathfindingBreadcrumb : IEquatable<PathfindingBreadcrumb>
{
    /// <summary>
    /// The X and Y index in the point grid.
    /// The actual coordinates require using <see cref="SharedPathfindingSystem.ChunkSize"/> and <see cref="SharedPathfindingSystem.SubStep"/>
    /// </summary>
    public Vector2i Coordinates;

    public PathfindingData Data;

    public static readonly PathfindingBreadcrumb Invalid = new()
    {
        Data = new PathfindingData(PathfindingBreadcrumbFlag.None, -1, -1, 0f, 0f), // RimFortress
    };

    public PathfindingBreadcrumb(Vector2i coordinates, int layer, int mask, float damage, float baseCost, PathfindingBreadcrumbFlag flags = PathfindingBreadcrumbFlag.None)
    {
        Coordinates = coordinates;
        Data = new PathfindingData(flags, layer, mask, damage, baseCost); // RimFortress
    }

    /// <summary>
    /// Is this crumb equal for pathfinding region purposes.
    /// </summary>
    public bool Equivalent(PathfindingBreadcrumb other)
    {
        return Data.Equals(other.Data);
    }

    public bool Equals(PathfindingBreadcrumb other)
    {
        return Coordinates.Equals(other.Coordinates) && Data.Equals(other.Data);
    }

    public override bool Equals(object? obj)
    {
        return obj is PathfindingBreadcrumb other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Coordinates, Data);
    }
}

/// <summary>
/// The data relevant for pathfinding.
/// </summary>
[Serializable, NetSerializable]
public struct PathfindingData : IEquatable<PathfindingData>
{
    public PathfindingBreadcrumbFlag Flags;
    public int CollisionLayer;
    public int CollisionMask;
    public float Damage;
    public float BaseCost; // RimFortress

    public bool IsFreeSpace => (Flags == PathfindingBreadcrumbFlag.None && Damage.Equals(0f));

    public PathfindingData(PathfindingBreadcrumbFlag flag, int layer, int mask, float damage, float baseCost) // RimFortress
    {
        Flags = flag;
        CollisionLayer = layer;
        CollisionMask = mask;
        Damage = damage;
        BaseCost = baseCost; // RimFortress
    }

    public bool IsEquivalent(PathfindingData other)
    {
        return CollisionLayer.Equals(other.CollisionLayer) &&
               CollisionMask.Equals(other.CollisionMask) &&
               Flags.Equals(other.Flags);
    }

    public bool Equals(PathfindingData other)
    {
        return CollisionLayer.Equals(other.CollisionLayer) &&
               CollisionMask.Equals(other.CollisionMask) &&
               Flags.Equals(other.Flags) &&
               Damage.Equals(other.Damage)
               && BaseCost.Equals(other.BaseCost);
    }

    public override bool Equals(object? obj)
    {
        return obj is PathfindingData other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine((int) Flags, CollisionLayer, CollisionMask);
    }
}

[Flags]
public enum PathfindingBreadcrumbFlag : ushort
{
    None = 0,

    /// <summary>
    /// Has this poly been replaced and is it no longer valid.
    /// </summary>
    Invalid = 1 << 0,
    Space = 1 << 1,

    /// <summary>
    /// Is there a door that is potentially pryable
    /// </summary>
    Door = 1 << 2,

    /// <summary>
    /// Is there access required
    /// </summary>
    Access = 1 << 3,

    /// <summary>
    /// Is there climbing involved
    /// </summary>
    Climb = 1 << 4,
}
