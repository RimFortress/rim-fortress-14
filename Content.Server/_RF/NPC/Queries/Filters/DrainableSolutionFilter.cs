using Content.Server.NPC;
using Content.Shared.Chemistry.EntitySystems;

namespace Content.Server._RF.NPC.Queries.Filters;

public sealed partial class DrainableSolutionFilter : RfUtilityQueryFilter
{
    private SharedSolutionContainerSystem _solution;

    [DataField]
    public float? MaxVolumeMoreThan;

    [DataField]
    public float? MaxVolumeLessThan;

    public override void Initialize(IEntityManager entManager)
    {
        base.Initialize(entManager);
        _solution = entManager.System<SharedSolutionContainerSystem>();
    }

    public override bool Filter(EntityUid uid, NPCBlackboard blackboard)
    {
        return _solution.TryGetDrainableSolution(uid, out _, out var solution)
               && (MaxVolumeMoreThan != null && solution.MaxVolume > MaxVolumeMoreThan
                   || MaxVolumeLessThan != null && solution.MaxVolume < MaxVolumeLessThan);
    }
}
