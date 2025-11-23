using Content.Server.NPC;

namespace Content.Server._RF.NPC.Queries.Queries;

/// <summary>
/// It just returns the entity that uses this query.
/// </summary>
public sealed partial class SelfQuery : RfUtilityQuery
{
    public override HashSet<EntityUid> Query(NPCBlackboard blackboard)
    {
        return new() { blackboard.GetValue<EntityUid>(NPCBlackboard.Owner) };
    }
}
