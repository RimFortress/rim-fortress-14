using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._RF.NPC;

public abstract class SharedNpcControlSystem : EntitySystem
{

}

public sealed class NpcTask
{
    public NpcTaskType Type { get; }
    public EntityCoordinates? MoveTo { get; }
    public EntityUid? Attack { get; }

    public NpcTask(EntityCoordinates coordinates)
    {
        Type = NpcTaskType.Move;
        MoveTo = coordinates;
    }

    public NpcTask(EntityUid uid)
    {
        Type = NpcTaskType.Attack;
        Attack = uid;
    }

    public NpcTask(NpcTaskType type, EntityCoordinates? coordinates, EntityUid? attack)
    {
        Type = type;
        MoveTo = coordinates;
        Attack = attack;
    }
}

public enum NpcTaskType : byte
{
    Move,
    Attack,
}

[Serializable, NetSerializable]
public sealed class NpcTaskInfoMessage : EntityEventArgs
{
    public NetEntity Entity { get; set; }
    public NpcTaskType TaskType { get; set; }
    public NetCoordinates? MoveTo { get; set; }
    public NetEntity? Attack { get; set; }
}

[Serializable, NetSerializable]
public sealed class NpcTaskResetRequest : EntityEventArgs
{
    public NetEntity Requester { get; set; }
    public NetEntity Entity { get; set; }
}

[Serializable, NetSerializable]
public sealed class NpcMoveToRequest : EntityEventArgs
{
    public NetEntity Requester { get; set; }
    public NetEntity Entity { get; set; }
    public NetCoordinates Target { get; set; }
}

[Serializable, NetSerializable]
public sealed class NpcAttackRequest : EntityEventArgs
{
    public NetEntity Requester { get; set; }
    public NetEntity Entity { get; set; }
    public NetEntity Attack { get; set; }
}
