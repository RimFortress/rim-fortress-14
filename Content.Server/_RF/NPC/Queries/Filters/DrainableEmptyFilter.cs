using Content.Server.NPC;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;

namespace Content.Server._RF.NPC.Queries.Filters;

public sealed partial class DrainableEmptyFilter : RfUtilityQueryFilter
{
    private SharedSolutionContainerSystem _solution;

    public override void Initialize(IEntityManager entManager)
    {
        base.Initialize(entManager);
        _solution = entManager.System<SharedSolutionContainerSystem>();
    }

    public override bool Filter(EntityUid uid, NPCBlackboard blackboard)
    {
        return _solution.TryGetDrainableSolution(uid, out _, out var solution)
               && solution.Volume == FixedPoint2.Zero;
    }
}
