using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._RF.NPC;

public abstract class SharedNPCControlSystem : EntitySystem
{

}

public sealed class NPCTask
{
    public NPCTaskType Type { get; }
    public EntityCoordinates? MoveTo { get; }
    public EntityUid? Attack { get; }

    public NPCTask(EntityCoordinates coordinates)
    {
        Type = NPCTaskType.Move;
        MoveTo = coordinates;
    }

    public NPCTask(EntityUid uid)
    {
        Type = NPCTaskType.Attack;
        Attack = uid;
    }

    public NPCTask(NPCTaskType type, EntityCoordinates? coordinates, EntityUid? attack)
    {
        Type = type;
        MoveTo = coordinates;
        Attack = attack;
    }
}

public enum NPCTaskType : byte
{
    Move,
    Attack,
}

[Serializable, NetSerializable]
public sealed class NPCTaskInfoMessage : EntityEventArgs
{
    public NetEntity Entity { get; set; }
    public NPCTaskType TaskType { get; set; }
    public NetCoordinates? MoveTo { get; set; }
    public NetEntity? Attack { get; set; }
}

[Serializable, NetSerializable]
public sealed class NPCTaskResetRequest : EntityEventArgs
{
    public NetEntity Entity { get; set; }
}

[Serializable, NetSerializable]
public sealed class NPCMoveToRequest : EntityEventArgs
{
    public NetEntity Entity { get; set; }
    public NetCoordinates Target { get; set; }
}

[Serializable, NetSerializable]
public sealed class NPCAttackRequest : EntityEventArgs
{
    public NetEntity Entity { get; set; }
    public NetEntity Attack { get; set; }
}
