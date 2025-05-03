using Content.Server.NPC;
using Content.Shared.Hands.Components;
using Content.Shared.Wieldable.Components;

namespace Content.Server._RF.NPC.HTN.Preconditions;

/// <summary>
/// Returns true if the item is wielded
/// </summary>
public sealed partial class WieldedPrecondition : InvertiblePrecondition
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    public override bool IsMetInvertible(NPCBlackboard blackboard)
    {
        return blackboard.TryGetValue<Hand>(NPCBlackboard.ActiveHand, out var hand, _entManager)
               && hand.HeldEntity is { } entity
               && _entManager.TryGetComponent(entity, out WieldableComponent? wield)
               && wield.Wielded;
    }
}
