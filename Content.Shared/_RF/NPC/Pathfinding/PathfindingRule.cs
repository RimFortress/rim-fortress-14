using Content.Shared._RF.MathHelpers.MathCurve;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared.EntityConditions;
using Content.Shared._RF.NPC.Pathfinding.Prototypes;
using Content.Shared._RF.NPC.UtilityAi.Prototypes;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.NPC.Pathfinding;

/// <summary>
/// The rule describing how to calculate the cost of each tile when finding a path.
/// </summary>
[DataDefinition]
public sealed partial class PathfindingRule
{
    /// <summary>
    /// Conditions related to a path node that must be met for the rule to be applied.
    /// </summary>
    [DataField("when")]
    public List<PathCondition> Conditions = new();

    /// <summary>
    /// Conditions that apply to the agent performing the path search,
    /// which must be met for the rule to be applied.
    /// </summary>
    [DataField("requires")]
    public List<GoapCondition> AgentConditions = new();

    /// <summary>
    /// The cost formula that will be added to the path node when the rule is applied.
    /// The result is added to the node's cost modifier, which is multiplied by the distance traveled.
    /// </summary>
    [DataField]
    public List<MathCurve> Cost = new();

    /// <summary>
    /// The UAI goal that must be performed at the path node that satisfies the conditions of this rule.
    /// The cost of this goal will be added to the node's cost.
    /// </summary>
    [DataField]
    public PathRuleGoal? Goal;
}

/// <summary>
/// The UAI goal that a <see cref="PathfindingRule"/> gives to the path node, and what it needs to be performed.
/// </summary>
[DataDefinition]
public sealed partial class PathRuleGoal
{
    /// <summary>
    /// The UAI goal that must be performed at the path node.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<UtilityAiGoalPrototype> Id;

    /// <summary>
    /// A key to which the coordinates of the path node for executing the goal will be written in the agent's state.
    /// </summary>
    [DataField]
    public StateKey<EntityCoordinates> TargetKey = "TargetCoordinates";

    /// <summary>
    /// A key in the agent's state from which the distance is taken at which the agent stops before the path node
    /// to perform the goal. By default it is the same range the movement actions use.
    /// </summary>
    [DataField]
    public StateKey<float> RangeKey = GoapState.MovementRange;

    /// <summary>
    /// When the goal is performed: before the agent goes through the node, or after.
    /// There can be one goal of each kind at a node, the first rule of the kind gives it.
    /// </summary>
    [DataField("when")]
    public PathGoalPhase Phase = PathGoalPhase.Before;

    /// <summary>
    /// The path tag of the entity at the node that the goal is for. The entity with it, found at the node, is written to
    /// the <see cref="EntityKey"/>.
    /// </summary>
    [DataField]
    public ProtoId<PathTagPrototype>? EntityTag;

    /// <summary>
    /// What the entity at the node that the goal is for has to be like, the conditions that it has to meet, in addition to
    /// <see cref="EntityTag"/> if it is set. The agent is the source of the conditions.
    /// </summary>
    [DataField]
    public EntityCondition[] EntityConditions = [];

    /// <summary>
    /// A key to which the entity at the node is written in the agent's state, for the goals that are for an entity,
    /// such as the one that opens a door. The place is not enough for them.
    /// </summary>
    [DataField]
    public StateKey<EntityUid>? EntityKey;
}
