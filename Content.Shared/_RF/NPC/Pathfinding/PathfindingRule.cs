using Content.Shared._RF.MathHelpers.MathCurve;
using Content.Shared._RF.NPC.GOAP;
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
    public ProtoId<UtilityAiGoalPrototype>? Goal;

    /// <summary>
    /// A key to which the coordinates of the path node for executing the
    /// <see cref="Goal"/> will be written in the agent's state.
    /// </summary>
    [DataField]
    public StateKey<EntityCoordinates> GoalTargetKey = "TargetCoordinates";

    /// <summary>
    /// A key in the agent's state from which the distance is taken at which the agent stops before the path node
    /// to perform the <see cref="Goal"/>. By default it is the same range the movement actions use.
    /// </summary>
    [DataField]
    public StateKey<float> GoalRangeKey = GoapState.MovementRange;
}
