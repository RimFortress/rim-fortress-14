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
    /// The tag is <b>dynamic</b>: it is not baked into the navigation map, because what has it moves or changes (the residents of a
    /// settlement, those who shoot), but the rules of a profile use it the same way: the influence is found out for a node when the
    /// path is searched for, from where the entities that have the tag are at the time. The profile does not tell the two kinds apart,
    /// <c>TagInfluence</c> and <c>HasTag</c> work for both. How much a source counts for, how far, and whether it has to be seen is the
    /// profile's to say, in the condition of its rule. The entities that have the tag are not the navigation map's concern, so
    /// <see cref="MakesObstacle"/> is of no use to the dynamic tag.
    /// </summary>
    [DataField]
    public bool Dynamic;

    /// <summary>
    /// Whether the solid bodies that have the tag are obstacles that the agents keep away from, which is to say, whether
    /// their tiles are sources of the obstacle tag. A body that can be climbed onto, such as a table, is not one: the way
    /// to it is from the side, and a profile of those who do not climb has the rules for it by its own tag.
    /// </summary>
    [DataField]
    public bool MakesObstacle = true;
}
