using Content.Shared._RF.MathHelpers.MathCurve;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.NPC.Pathfinding.Prototypes;

/// <summary>
/// This is a prototype of a tag used to mark specific path-finding nodes.
/// </summary>
[Prototype]
public sealed partial class PathTagPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; set; } = default!;

    /// <summary>
    /// The radius within which the tag takes effect. A value of 0
    /// means that the tag applies only to the tile on which it is located.
    /// </summary>
    [DataField]
    public float Influence;

    /// <summary>
    /// The formula used to calculate the effect of this tag on
    /// adjacent tiles within the <see cref="Influence"/> radius.
    /// The distance of the tile from the tag's source is passed as input.
    /// </summary>
    [DataField]
    public List<MathCurve> InfluenceCurve = new();

    /// <summary>
    /// Whether the solid bodies that have the tag are obstacles that the agents keep away from, which is to say, whether
    /// their tiles are sources of the obstacle tag. A body that can be climbed onto, such as a table, is not one: the way
    /// to it is from the side, and a profile of those who do not climb has the rules for it by its own tag.
    /// </summary>
    [DataField]
    public bool MakesObstacle = true;
}
