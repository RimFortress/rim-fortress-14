using System.Linq;
using Content.Server.NPC;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.Queries.Filters;

public sealed partial class RefillableReagentFilter : RfUtilityQueryFilter
{
    private SharedSolutionContainerSystem _solution;

    [DataField(required: true)]
    public ProtoId<ReagentPrototype> Reagent;

    [DataField]
    public float? MoreThan;

    [DataField]
    public float? LessThan;

    public override void Initialize(IEntityManager entManager)
    {
        base.Initialize(entManager);
        _solution = entManager.System<SharedSolutionContainerSystem>();
    }

    public override bool Filter(EntityUid uid, NPCBlackboard blackboard)
    {
        return _solution.TryGetRefillableSolution(uid, out _, out var solution)
            && solution.Contents.FirstOrDefault(x => x.Reagent.Prototype == Reagent) is { } reagent
            && (MoreThan != null && reagent.Quantity > MoreThan || LessThan != null && reagent.Quantity < LessThan);
    }
}
