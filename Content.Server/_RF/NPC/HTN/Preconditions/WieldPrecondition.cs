using Content.Server.NPC;
using Content.Shared.Hands.Components;
using Content.Shared.Wieldable.Components;

namespace Content.Server._RF.NPC.HTN.Preconditions;

/// <summary>
/// Returns true if the item in hands is wielded
/// </summary>
public sealed partial class WieldedPrecondition : InvertiblePrecondition
{
    public override bool IsMetInvertible(NPCBlackboard blackboard)
    {
        return blackboard.TryGetValue<Hand>(NPCBlackboard.ActiveHand, out var hand, EntityManager)
               && hand.HeldEntity is { } entity
               && EntityManager.TryGetComponent(entity, out WieldableComponent? wield)
               && wield.Wielded;
    }
}
