using Content.Server.NPC;
using Robust.Server.Containers;

namespace Content.Server._RF.NPC.HTN.Preconditions.Stockpile;

public sealed partial class TargetInContainerPrecondition : InvertiblePrecondition
{
    private ContainerSystem _container = default!;

    [DataField(required: true)]
    public string TargetKey;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _container = sysManager.GetEntitySystem<ContainerSystem>();
    }

    public override bool IsMetInvertible(NPCBlackboard blackboard)
    {
        return blackboard.TryGetValue(TargetKey, out EntityUid target, EntityManager)
               && _container.IsEntityOrParentInContainer(target);
    }
}
