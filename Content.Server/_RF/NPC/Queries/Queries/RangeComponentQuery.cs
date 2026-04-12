using Content.Server.NPC;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.Queries.Queries;

/// <summary>
/// Filters entities within a specified radius by the presence of given components.
/// </summary>
public sealed partial class RangeComponentQuery : RfUtilityQuery
{
    private TransformSystem _transform;
    private EntityLookupSystem _lookup;

    /// <summary>
    /// Components to be filtered out.
    /// </summary>
    [DataField(required: true)]
    public ComponentRegistry Components = default!;

    /// <summary>
    /// The radius in which the entities will be searched for.
    /// </summary>
    [DataField]
    public float Range = 50f;

    /// <summary>
    /// The coordinates around which entities will be searched within a specified radius.
    /// </summary>
    [DataField]
    public string StartCoordinatesKey = NPCBlackboard.OwnerCoordinates;

    public override void Initialize(IEntityManager entManager)
    {
        base.Initialize(entManager);

        _transform = entManager.System<TransformSystem>();
        _lookup = entManager.System<EntityLookupSystem>();
    }

    public override HashSet<EntityUid> Query(NPCBlackboard blackboard)
    {
        if (Components.Count == 0
            || blackboard.TryGetValue<EntityCoordinates>(StartCoordinatesKey, out var coords, EntityManager))
            return new();

        var query = new HashSet<EntityUid>();
        var entities = new HashSet<Entity<IComponent>>();
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        var compTypes = new List<EntityPrototype.ComponentRegistryEntry>();
        var i = -1;

        EntityPrototype.ComponentRegistryEntry compZero = default!;

        foreach (var compType in Components.Values)
        {
            i++;

            if (i == 0)
            {
                compZero = compType;
                continue;
            }

            compTypes.Add(compType);
        }

        _lookup.GetEntitiesInRange(compZero.Component.GetType(), _transform.ToMapCoordinates(coords), Range, entities);

        foreach (var comp in entities)
        {
            if (comp.Owner == owner)
                continue;

            var othersFound = true;

            foreach (var compOther in compTypes)
            {
                if (EntityManager.HasComponent(comp.Owner, compOther.Component.GetType()))
                    continue;

                othersFound = false;
                break;
            }

            if (!othersFound)
                continue;

            query.Add(comp.Owner);
        }

        return query;
    }
}
