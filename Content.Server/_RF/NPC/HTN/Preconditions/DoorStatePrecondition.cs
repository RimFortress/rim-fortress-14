using Content.Server.NPC;
using Content.Server.NPC.HTN.Preconditions;
using Content.Shared.Doors.Components;

namespace Content.Server._RF.NPC.HTN.Preconditions;

/// <summary>
/// Checks the current state of the door
/// </summary>
public sealed partial class DoorStatePrecondition : HTNPrecondition
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    [DataField(required: true)]
    public string Key = default!;

    [DataField(required: true)]
    public DoorState State;

    [DataField]
    public bool Invert;

    public override bool IsMet(NPCBlackboard blackboard)
    {
        if (blackboard.TryGetValue<EntityUid>(Key, out var entity, _entManager)
            && _entManager.TryGetComponent(entity, out DoorComponent? door)
            && door.State == State)
            return !Invert;

        return Invert;
    }
}
