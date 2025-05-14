using Content.Server._RF.NPC.Components;
using Content.Server.NPC;
using Content.Shared._RF.NPC;

namespace Content.Server._RF.NPC.Queries.Queries;

public sealed partial class OwnedQuery : RfUtilityQuery
{
    public override HashSet<EntityUid> Query(NPCBlackboard blackboard)
    {
        var query = new HashSet<EntityUid>();
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!EntityManager.TryGetComponent(owner, out ControllableNpcComponent? control)
            || control.CanControl.Count == 0)
            return query;

        var entities = EntityManager.EntityQueryEnumerator<OwnedComponent>();
        while (entities.MoveNext(out var uid, out var comp))
        {
            var valid = false;

            foreach (var canControl in control.CanControl)
            {
                if (comp.Owners.Contains(canControl))
                    valid = true;
            }

            if (!valid)
                continue;

            query.Add(uid);
        }

        return query;
    }
}
