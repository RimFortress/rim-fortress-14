using Content.Server.NPC;
using Content.Shared._RF.NPC;

namespace Content.Server._RF.NPC.Queries.Queries;

public sealed partial class OwnedQuery : RfUtilityQuery
{
    private OwnershipSystem _ownership;

    public override void Initialize(IEntityManager entManager)
    {
        base.Initialize(entManager);
        _ownership = entManager.System<OwnershipSystem>();
    }

    public override HashSet<EntityUid> Query(NPCBlackboard blackboard)
    {
        var query = new HashSet<EntityUid>();

        foreach (var uid in _ownership.GetOwners(blackboard.GetOwner()))
        {
            query.UnionWith(_ownership.GetOwned(uid));
        }

        return query;
    }
}
