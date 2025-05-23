using Content.Server.NPC;
using Content.Shared.Hands.Components;
using Content.Shared.Weapons.Ranged.Components;

namespace Content.Server._RF.NPC.HTN.Preconditions;

/// <summary>
/// Returns true if the bolt of the weapon in the active hand is closed
/// </summary>
public sealed partial class BoltClosedPrecondition : InvertiblePrecondition
{
    /// <summary>
    /// The value that will be returned if the weapon has no bolt.
    /// </summary>
    [DataField]
    public bool NoBoltValue;

    public override bool IsMetInvertible(NPCBlackboard blackboard)
    {
        return blackboard.TryGetValue<Hand>(NPCBlackboard.ActiveHand, out var hand, EntityManager)
               && hand.HeldEntity is { } entity
               && EntityManager.TryGetComponent(entity, out ChamberMagazineAmmoProviderComponent? chamber)
               && chamber.BoltClosed != null;
    }
}
