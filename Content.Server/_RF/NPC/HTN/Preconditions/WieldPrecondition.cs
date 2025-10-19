using Content.Server.NPC;
using Content.Shared.Wieldable.Components;

namespace Content.Server._RF.NPC.HTN.Preconditions;

/// <summary>
/// Returns true if the item in hands is wielded
/// </summary>
public sealed partial class WieldedPrecondition : InvertiblePrecondition
{
    public override bool IsMetInvertible(NPCBlackboard blackboard)
    {
        return blackboard.TryGetValue<EntityUid>(NPCBlackboard.ActiveHandEntity, out var entity, EntityManager)
               && EntityManager.TryGetComponent(entity, out WieldableComponent? wield)
               && wield.Wielded;
    }
}
