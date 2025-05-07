using Content.Server.NPC;
using Content.Shared.Stacks;

namespace Content.Server._RF.NPC.Queries.Filters;

public sealed partial class MaterialKeyFilter : RfUtilityQueryFilter
{
    private EntityQuery<StackComponent> _stackQuery;

    /// <summary>
    /// Key containing the material to be filtered out
    /// </summary>
    [DataField(required: true)]
    public string TargetKey = default!;

    /// <summary>
    /// Key containing the minimum amount of material that will be filtered out
    /// </summary>
    [DataField(required: true)]
    public string AmountKey = default!;

    private string? _stackId;
    private int _amount;

    public override void Initialize(IEntityManager entManager)
    {
        base.Initialize(entManager);
        _stackQuery = entManager.GetEntityQuery<StackComponent>();
    }

    public override bool Startup(NPCBlackboard blackboard)
    {
        return blackboard.TryGetValue(TargetKey, out _stackId, EntityManager)
               && blackboard.TryGetValue(AmountKey, out _amount, EntityManager);
    }

    public override bool Filter(EntityUid uid, NPCBlackboard blackboard)
    {
        return _stackQuery.TryComp(uid, out var comp)
               && comp.StackTypeId == _stackId
               && comp.Count >= _amount;
    }
}
