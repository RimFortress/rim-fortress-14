using Content.Server._RF.NPC.Components;
using Content.Server.NPC;
using Content.Shared._RF.NPC;

namespace Content.Server._RF.NPC.HTN.Preconditions;

/// <summary>
/// Checks if at least one of the target's owners matches the owner of the NPC
/// </summary>
public sealed partial class OwnedPrecondition : InvertiblePrecondition
{
    private OwnershipSystem _ownership;
    private EntityQuery<ControllableNpcComponent> _controlledQuery;

    [DataField(required: true)]
    public string TargetKey;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _ownership = sysManager.GetEntitySystem<OwnershipSystem>();
        _controlledQuery = EntityManager.GetEntityQuery<ControllableNpcComponent>();
    }

    public override bool IsMetInvertible(NPCBlackboard blackboard)
    {
        var owner = blackboard.GetValueOrDefault<EntityUid>(NPCBlackboard.Owner, EntityManager);

        if (!blackboard.TryGetValue(TargetKey, out EntityUid? target, EntityManager)
            || !_controlledQuery.TryGetComponent(owner, out var control))
            return false;

        foreach (var uid in control.CanControl)
        {
            if (_ownership.HasOwner(target.Value, uid))
                return true;
        }

        return false;
    }
}
