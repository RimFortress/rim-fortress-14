using Content.Server.NPC;
using Content.Shared.Doors.Components;

namespace Content.Server._RF.NPC.HTN.Preconditions;

/// <summary>
/// Checks the current state of the door
/// </summary>
public sealed partial class DoorStatePrecondition : InvertiblePrecondition
{
    [DataField(required: true)]
    public string Key = default!;

    [DataField(required: true)]
    public DoorState State;

    public override bool IsMetInvertible(NPCBlackboard blackboard)
    {
        return blackboard.TryGetValue<EntityUid>(Key, out var entity, EntityManager)
               && EntityManager.TryGetComponent(entity, out DoorComponent? door)
               && door.State == State;
    }
}
