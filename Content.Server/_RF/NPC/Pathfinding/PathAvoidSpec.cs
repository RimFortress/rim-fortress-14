using Content.Shared._RF.NPC.Search.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.Pathfinding;

/// <summary>
/// The places that the path of an agent keeps away from, taken from what the agent finds with a search query: every entity of the result
/// is a place, and the nodes near it cost more, the closer the more. Which entities there are is up to the query, so it is the agent's
/// own idea of whom to keep away from, and not the same for everybody (the enemies of the agent, those who have hit it).
/// See <see cref="Systems.RfPathfindingSystem.GetAvoid"/>.
/// </summary>
[DataDefinition]
public sealed partial class PathAvoidSpec
{
    /// <summary>
    /// The search query whose results are kept away from.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<SearchQueryPrototype> Query;

    /// <summary>
    /// How far from each of them the nodes cost more, in tiles.
    /// </summary>
    [DataField]
    public float Radius = 18f;

    /// <summary>
    /// How much more a node costs next to one of them, it falls to nothing at the <see cref="Radius"/>.
    /// </summary>
    [DataField]
    public float Cost = 30f;
}
