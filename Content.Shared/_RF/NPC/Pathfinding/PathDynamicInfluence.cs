using System.Numerics;
using Content.Shared._RF.NPC.Pathfinding.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.NPC.Pathfinding;

/// <summary>
/// A dynamic path tag (<see cref="PathTagPrototype.Dynamic"/>) as the pathfinding knows it: the shape of the influence, the same as of
/// a baked tag, and the entities that have the tag.
/// </summary>
public sealed class PathDynamicTag(ProtoId<PathTagPrototype> id, float[] shape, float radius)
{
    public readonly ProtoId<PathTagPrototype> Id = id;

    /// <summary>
    /// How strong the influence is by the distance from the source, in steps of <c>DistanceStep</c>, 0 to 1.
    /// </summary>
    public readonly float[] Shape = shape;

    /// <summary>
    /// The radius that the <see cref="Shape"/> is for, in tiles.
    /// </summary>
    public readonly float Radius = radius;

    /// <summary>
    /// The entities that have the tag.
    /// </summary>
    public readonly HashSet<EntityUid> Sources = new();
}

/// <summary>
/// Where the sources of the dynamic tags are for a path request: they are found out once, when the request begins, and the
/// influence on a node is made of them when the node is reached, so it is not the navigation map that changes when they move.
/// </summary>
public sealed class PathDynamicInfluence(EntityUid graph)
{
    /// <summary>
    /// The grid that the positions are in.
    /// </summary>
    public readonly EntityUid Graph = graph;

    /// <summary>
    /// Where the entities with the tags are, in the coordinates of the grid.
    /// </summary>
    public readonly Dictionary<ProtoId<PathTagPrototype>, List<Vector2>> Positions = new();

    /// <summary>
    /// The sources of the conditions (<c>TagInfluence</c>) of the rules, with what each counts for according to the condition.
    /// </summary>
    public readonly Dictionary<Conditions.TagInfluence, List<Source>> Sources = new();

    /// <summary>
    /// A source of a condition.
    /// </summary>
    /// <param name="Position">Where it is, in the coordinates of the grid.</param>
    /// <param name="Weight">What it counts for.</param>
    /// <param name="Radius">How far it influences, in tiles.</param>
    public readonly record struct Source(Vector2 Position, float Weight, float Radius);
}
