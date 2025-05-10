using System.Linq;
using Content.Server.NPC;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.Queries.Filters;

public sealed partial class DrainableOnlyReagentFilter : RfUtilityQueryFilter
{
    private SharedSolutionContainerSystem _solution;

    [DataField(required: true)]
    public ProtoId<ReagentPrototype> Reagent;

    public override void Initialize(IEntityManager entManager)
    {
        base.Initialize(entManager);
        _solution = entManager.System<SharedSolutionContainerSystem>();
    }

    public override bool Filter(EntityUid uid, NPCBlackboard blackboard)
    {
        return _solution.TryGetDrainableSolution(uid, out _, out var solution)
            && solution.Contents.All(x => x.Reagent.Prototype == Reagent || x.Quantity == FixedPoint2.Zero);
    }
}
