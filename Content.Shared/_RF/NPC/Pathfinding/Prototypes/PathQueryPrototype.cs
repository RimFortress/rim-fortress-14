using Content.Shared._RF.NPC.Pathfinding.Algorithms;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.NPC.Pathfinding.Prototypes;

/// <summary>
/// A kind of request for a path: what is looked for and how, for example a way to a place, or a place to wander to.
/// The profile of the agent says what costs what, the query says what to do with it. The rules of a profile can apply
/// to some queries and not to the others, see <see cref="Conditions.Query"/>.
/// </summary>
[Prototype]
public sealed partial class PathQueryPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; set; } = default!;

    /// <summary>
    /// The algorithm that searches for the path and its settings: how far it goes, what the path may cost at the most,
    /// whether a path that does not get to the target is a result.
    /// </summary>
    [DataField]
    public PathAlgorithm Algorithm = new AStar();
}
