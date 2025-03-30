using Content.Server.NPC;
using Content.Server.NPC.HTN.Preconditions;
using Content.Shared.Hands.Components;
using Content.Shared.Wieldable.Components;

namespace Content.Server._RF.NPC.HTN.Preconditions;

/// <summary>
/// Returns true if the item is wielded
/// </summary>
public sealed partial class WieldedPrecondition : HTNPrecondition
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    [DataField]
    public bool Invert;

    public override bool IsMet(NPCBlackboard blackboard)
    {
        if (!blackboard.TryGetValue<Hand>(NPCBlackboard.ActiveHand, out var hand, _entManager)
            || hand.HeldEntity is not { } entity
            || !_entManager.TryGetComponent(entity, out WieldableComponent? wield))
            return Invert;

        if (Invert)
            return !wield.Wielded;

        return wield.Wielded;
    }
}
