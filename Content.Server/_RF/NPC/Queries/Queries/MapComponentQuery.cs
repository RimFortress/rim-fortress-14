using System.Linq;
using Content.Server.NPC;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.Queries.Queries;


public sealed partial class MapComponentQuery : RfUtilityQuery
{
    private EntityQuery<TransformComponent> _xformQuery;

    /// <summary>
    /// Components to be filtered out
    /// </summary>
    [DataField(required: true)]
    public ComponentRegistry Components = default!;

    public override void Initialize(IEntityManager entManager)
    {
        base.Initialize(entManager);
        _xformQuery = entManager.GetEntityQuery<TransformComponent>();
    }

    public override HashSet<EntityUid> Query(NPCBlackboard blackboard)
    {
        var query = new HashSet<EntityUid>();
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        var mapId = _xformQuery.GetComponent(owner).MapID;

        // It's clearly not the most optimized option
        var enumerator = EntityManager.AllEntityQueryEnumerator(Components.Values.First().Component.GetType());
        while (enumerator.MoveNext(out var uid, out _))
        {
            if (!_xformQuery.TryComp(uid, out var xform) || xform.MapID != mapId)
                continue;

            var hasComp = true;
            foreach (var comp in Components.Values)
            {
                if (EntityManager.HasComponent(uid, comp.Component.GetType()))
                    continue;

                hasComp = false;
                break;
            }

            if (!hasComp)
                continue;

            query.Add(uid);
        }

        return query;
    }
}
