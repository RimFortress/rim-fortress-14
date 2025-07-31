using Content.Server.NPC;
using Robust.Server.Containers;

namespace Content.Server._RF.NPC.Queries.Filters;

public sealed partial class InContainerFilter : RfUtilityQueryFilter
{
    private ContainerSystem _container = default!;

    public override void Initialize(IEntityManager entManager)
    {
        base.Initialize(entManager);
        _container = entManager.System<ContainerSystem>();
    }

    public override bool Filter(EntityUid uid, NPCBlackboard blackboard)
    {
        return _container.IsEntityOrParentInContainer(uid);
    }
}
