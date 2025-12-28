using Content.Server.NPC;
using Content.Shared.Nutrition.EntitySystems;

namespace Content.Server._RF.NPC.HTN.Preconditions;

/// <summary>
/// Checks whether the target entity is closed
/// </summary>
public sealed partial class ClosedPrecondition : InvertiblePrecondition
{
    [DataField]
    public string TargetKey = "Target";

    private OpenableSystem _openable;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _openable = sysManager.GetEntitySystem<OpenableSystem>();
    }

    public override bool IsMetInvertible(NPCBlackboard blackboard)
        => blackboard.TryGetValue<EntityUid>(TargetKey, out var uid, EntityManager) && _openable.IsClosed(uid);
}
