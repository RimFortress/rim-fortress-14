using Content.Server.NPC;

namespace Content.Server._RF.NPC.Queries.Queries;


public sealed partial class ComponentKeyQuery : RfUtilityQuery
{
    /// <summary>
    /// Component to be filtered out
    /// </summary>
    [DataField(required: true)]
    public string Key = default!;

    public override HashSet<EntityUid> Query(NPCBlackboard blackboard)
    {
        if (!blackboard.TryGetValue(Key, out string? compId, EntityManager))
            return new();

        var query = new HashSet<EntityUid>();

        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        var comp = EntityManager.ComponentFactory.GetComponent(compId);

        var enumerator = EntityManager.AllEntityQueryEnumerator(comp.GetType());
        while (enumerator.MoveNext(out var uid, out _))
        {
            if (uid == owner)
                continue;

            query.Add(uid);
        }

        return query;
    }
}
