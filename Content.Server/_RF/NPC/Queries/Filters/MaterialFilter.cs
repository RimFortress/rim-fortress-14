using Content.Server.NPC;
using Content.Shared.Stacks;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.Queries.Filters;

public sealed partial class MaterialFilter : RfUtilityQueryFilter
{
    private EntityQuery<StackComponent> _stackQuery;

    /// <summary>
    /// The material to be filtered out
    /// </summary>
    [DataField(required: true)]
    public ProtoId<StackPrototype> Stack;

    /// <summary>
    /// The minimum amount of material that will be filtered out
    /// </summary>
    [DataField(required: true)]
    public int Amount;

    public override void Initialize(IEntityManager entManager)
    {
        base.Initialize(entManager);
        _stackQuery = entManager.GetEntityQuery<StackComponent>();
    }

    public override bool Filter(EntityUid uid, NPCBlackboard blackboard)
    {
        return _stackQuery.TryComp(uid, out var comp)
               && comp.StackTypeId == Stack
               && comp.Count >= Amount;
    }
}
