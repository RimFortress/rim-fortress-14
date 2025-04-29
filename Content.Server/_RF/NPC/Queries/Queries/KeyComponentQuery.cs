using Content.Server.NPC;
using Robust.Server.GameObjects;

namespace Content.Server._RF.NPC.Queries.Queries;


public sealed partial class KeyComponentQuery : RfUtilityQuery
{
    private TransformSystem _transform = default!;
    private EntityLookupSystem _lookup = default!;

    private EntityQuery<TransformComponent> _xformQuery;

    /// <summary>
    /// Component to be filtered out
    /// </summary>
    [DataField(required: true)]
    public string Key = default!;

    public override void Initialize(IEntityManager entManager)
    {
        base.Initialize(entManager);
        _transform = entManager.System<TransformSystem>();
        _lookup = entManager.System<EntityLookupSystem>();

        _xformQuery = entManager.GetEntityQuery<TransformComponent>();
    }

    public override HashSet<EntityUid> Query(NPCBlackboard blackboard)
    {
        if (!blackboard.TryGetValue(Key, out string? compId, EntityManager))
            return new();

        var query = new HashSet<EntityUid>();
        var entitySet = new HashSet<Entity<IComponent>>();

        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        var vision = blackboard.GetValueOrDefault<float>(blackboard.GetVisionRadiusKey(EntityManager), EntityManager);
        var mapPos = _transform.GetMapCoordinates(owner, xform: _xformQuery.GetComponent(owner));
        var comp = EntityManager.ComponentFactory.GetComponent(compId);

        _lookup.GetEntitiesInRange(comp.GetType(), mapPos, vision * 10, entitySet);

        foreach (var ent in entitySet)
        {
            if (ent.Owner == owner)
                continue;

            if (EntityManager.HasComponent(ent, comp.GetType()))
                query.Add(ent);
        }

        return query;
    }
}
