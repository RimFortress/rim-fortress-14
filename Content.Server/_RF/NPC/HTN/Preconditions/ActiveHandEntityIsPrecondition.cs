using Content.Server.NPC;

namespace Content.Server._RF.NPC.HTN.Preconditions;

/// <summary>
/// Checks if the NPC is holding a certain entity in hand
/// </summary>
public sealed partial class ActiveHandEntityIsPrecondition : InvertiblePrecondition
{
    [DataField(required: true)]
    public string TargetKey = default!;

    public override bool IsMetInvertible(NPCBlackboard blackboard)
    {
        return blackboard.TryGetValue(TargetKey, out EntityUid? entity, EntityManager)
               && blackboard.TryGetValue<EntityUid>(NPCBlackboard.ActiveHandEntity, out var heldEntity, EntityManager)
               && heldEntity == entity;
    }
}
