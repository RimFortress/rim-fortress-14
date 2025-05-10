using Content.Server.Medical.Components;
using Content.Server.NPC;

namespace Content.Server._RF.NPC.Queries.Filters;

/// <summary>
/// Filters items for healing by damage container and the type of damage it heals
/// </summary>
public sealed partial class BloodHealingFilter : RfUtilityQueryFilter
{
    private EntityQuery<HealingComponent> _healingQuery;

    public override void Initialize(IEntityManager entManager)
    {
        base.Initialize(entManager);
        _healingQuery = EntityManager.GetEntityQuery<HealingComponent>();
    }

    public override bool Filter(EntityUid uid, NPCBlackboard blackboard)
    {
        return _healingQuery.TryComp(uid, out var comp) && comp.ModifyBloodLevel > 0;
    }
}
