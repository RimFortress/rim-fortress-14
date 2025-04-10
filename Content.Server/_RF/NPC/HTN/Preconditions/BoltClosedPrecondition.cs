using Content.Server.NPC;
using Content.Server.NPC.HTN.Preconditions;
using Content.Shared.Hands.Components;
using Content.Shared.Weapons.Ranged.Components;

namespace Content.Server._RF.NPC.HTN.Preconditions;

/// <summary>
/// Returns true if the bolt of the weapon in the active hand is closed
/// </summary>
public sealed partial class BoltClosedPrecondition : HTNPrecondition
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    [DataField]
    public bool Invert;

    /// <summary>
    /// The value that will be returned if the weapon has no bolt.
    /// Inverting does not affect this
    /// </summary>
    [DataField]
    public bool NoBoltValue;

    public override bool IsMet(NPCBlackboard blackboard)
    {
        if (!blackboard.TryGetValue<Hand>(NPCBlackboard.ActiveHand, out var hand, _entManager)
            || hand.HeldEntity is not { } entity
            || !_entManager.TryGetComponent(entity, out ChamberMagazineAmmoProviderComponent? chamber))
        {
            return Invert;
        }

        if (chamber.BoltClosed == null)
            return NoBoltValue;

        if (Invert)
            return !chamber.BoltClosed.Value;

        return chamber.BoltClosed.Value;
    }
}
