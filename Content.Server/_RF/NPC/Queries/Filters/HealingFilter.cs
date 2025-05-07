using Content.Server.Medical.Components;
using Content.Server.NPC;

namespace Content.Server._RF.NPC.Queries.Filters;

/// <summary>
/// Filters items for healing by damage container and the type of damage it heals
/// </summary>
public sealed partial class HealingFilter : RfUtilityQueryFilter
{
    [DataField]
    public string DamageContainerKey = "DamageContainer";

    [DataField]
    public string DamageTypeKey = "DamageType";

    private EntityQuery<HealingComponent> _healingQuery;

    private string? _damageContainer;
    private string? _damageType;

    public override void Initialize(IEntityManager entManager)
    {
        base.Initialize(entManager);
        _healingQuery = EntityManager.GetEntityQuery<HealingComponent>();
    }

    public override bool Startup(NPCBlackboard blackboard)
    {
        return blackboard.TryGetValue(DamageContainerKey, out _damageContainer, EntityManager)
               && blackboard.TryGetValue(DamageTypeKey, out _damageType, EntityManager);
    }

    public override bool Filter(EntityUid uid, NPCBlackboard blackboard)
    {
        return _healingQuery.TryComp(uid, out var comp)
               && comp.DamageContainers != null
               && comp.DamageContainers.Contains(_damageContainer!)
               && comp.Damage.DamageDict.ContainsKey(_damageType!);
    }
}
