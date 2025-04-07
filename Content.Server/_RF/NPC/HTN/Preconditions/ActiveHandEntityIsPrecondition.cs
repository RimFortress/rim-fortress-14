using Content.Server.NPC;
using Content.Server.NPC.HTN.Preconditions;
using Content.Shared.Hands.Components;

namespace Content.Server._RF.NPC.HTN.Preconditions;

/// <summary>
/// Checks if the NPC is holding a certain entity in hand
/// </summary>
public sealed partial class ActiveHandEntityIsPrecondition : HTNPrecondition
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    [DataField(required: true)]
    public string TargetKey = default!;

    [DataField]
    public bool Invert;

    public override bool IsMet(NPCBlackboard blackboard)
    {
        if (!blackboard.TryGetValue(NPCBlackboard.ActiveHand, out Hand? activeHand, _entManager)
            || !blackboard.TryGetValue(TargetKey, out EntityUid? entity, _entManager)
            || activeHand.HeldEntity != entity)
            return Invert;

        return !Invert;
    }
}
