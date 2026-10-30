using Content.Shared._RF.NPC.Pathfinding.Prototypes;
using Content.Shared._RF.NPC.Pathfinding.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.NPC.Pathfinding.Conditions;

/// <summary>
/// The path is searched for with the query. Inverted, it means any other query, so a rule can apply to all the queries
/// but one, for example, a rule of water that a way may go through and a place to wander to may not.
/// </summary>
public sealed partial class Query : BasePathCondition<Query>
{
    /// <summary>
    /// The query.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<PathQueryPrototype> Id;
}

public sealed class QueryPathConditionSystem : PathConditionSystem<Query>
{
    protected override bool ConditionCheck(PathNodeContext ctx, Query condition) => ctx.Query == condition.Id;
}
