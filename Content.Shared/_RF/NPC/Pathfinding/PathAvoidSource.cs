using Content.Shared._RF.MathHelpers.MathCurve;
using Content.Shared._RF.NPC.GOAP;

namespace Content.Shared._RF.NPC.Pathfinding;

/// <summary>
/// Entities that the path keeps away from, as a profile says: the places around them cost more, the closer the more. The entities are
/// those in a key of the state of the agent, such as the results of a search query or the residents that a raid is for, and the
/// places are where they are when the path is searched for, so it is not the navigation map that changes when they move.
/// </summary>
[DataDefinition]
public sealed partial class PathAvoidSource
{
    /// <summary>
    /// The key of the state of the agent with the entities.
    /// </summary>
    [DataField(required: true)]
    public StateKey<IReadOnlyCollection<EntityUid>> Key;

    /// <summary>
    /// How far from an entity the nodes cost more.
    /// </summary>
    [DataField]
    public float Radius = 10f;

    /// <summary>
    /// How much more a node at an entity costs, it falls to nothing at the <see cref="Radius"/>. The curves are calculated with the
    /// entity as the user, so it can depend on what it is: how strong it is. Nothing is added if it is not more than 0.
    /// </summary>
    [DataField(required: true)]
    public List<MathCurve> Cost = new();

    /// <summary>
    /// What the agent has to be like for the entities to be avoided, the conditions of its state.
    /// </summary>
    [DataField("requires")]
    public List<GoapCondition> AgentConditions = new();
}
