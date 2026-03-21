using Content.Server._RF.NPC.Components;
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
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!EntityManager.TryGetComponent(owner, out ControllableNpcComponent? control)
            || control.CanControl.Count == 0)
            return query;

        var entities = EntityManager.EntityQueryEnumerator<OwnershipComponent>();
        while (entities.MoveNext(out var uid, out var comp))
        {
            var valid = false;

            foreach (var canControl in control.CanControl)
            {
                if (_ownership.HasOwner(new(uid, comp), canControl))
                    valid = true;
            }

            if (!valid)
                continue;

            query.Add(uid);
        }

        return query;
    }
}
