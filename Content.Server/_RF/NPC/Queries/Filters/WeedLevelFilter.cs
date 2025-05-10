using Content.Server.Botany.Components;
using Content.Server.NPC;

namespace Content.Server._RF.NPC.Queries.Filters;

public sealed partial class WeedLevelFilter : RfUtilityQueryFilter
{
    private EntityQuery<PlantHolderComponent> _plantHolderQuery;

    [DataField]
    public float? MoreThan;

    [DataField]
    public float? LessThan;

    public override void Initialize(IEntityManager entManager)
    {
        base.Initialize(entManager);
        _plantHolderQuery = entManager.GetEntityQuery<PlantHolderComponent>();
    }

    public override bool Filter(EntityUid uid, NPCBlackboard blackboard)
    {
        return _plantHolderQuery.TryComp(uid, out var comp)
               && (MoreThan != null && comp.WeedLevel > MoreThan || LessThan != null && comp.WeedLevel < LessThan);
    }
}
