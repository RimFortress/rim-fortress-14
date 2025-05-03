using Content.Server.NPC;
using Content.Shared.Hands.Components;

namespace Content.Server._RF.NPC.HTN.Preconditions;

/// <summary>
/// Checks if the NPC is holding a certain entity in hand
/// </summary>
public sealed partial class ActiveHandEntityIsPrecondition : InvertiblePrecondition
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    [DataField(required: true)]
    public string TargetKey = default!;

    public override bool IsMetInvertible(NPCBlackboard blackboard)
    {
        return blackboard.TryGetValue(NPCBlackboard.ActiveHand, out Hand? activeHand, _entManager)
               && blackboard.TryGetValue(TargetKey, out EntityUid? entity, _entManager)
               && activeHand.HeldEntity == entity;
    }
}
