using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._RF.NPC;

public abstract class SharedNpcControlSystem : EntitySystem
{

}

public sealed class NpcTask
{
    public NpcTaskType Type { get; }
    public EntityUid Target { get; }
    public EntityCoordinates Coordinates { get; }

    public NpcTask(NpcTaskType type, EntityUid target, EntityCoordinates coords)
    {
        Type = type;
        Target = target;
        Coordinates = coords;
    }
}

public enum NpcTaskType : byte
{
    Move,
    Attack,
    PickUp,
    Build,
    Pry,
}

[Serializable, NetSerializable]
public sealed class NpcTaskInfoMessage : EntityEventArgs
{
    public NetEntity Entity { get; set; }
    public NpcTaskType TaskType { get; set; }
    public NetEntity Target { get; set; }
    public NetCoordinates TargetCoordinates { get; set; }
}

[Serializable, NetSerializable]
public sealed class NpcTaskResetRequest : EntityEventArgs
{
    public NetEntity Requester { get; set; }
    public NetEntity Entity { get; set; }
}

[Serializable, NetSerializable]
public sealed class NpcTaskRequest : EntityEventArgs
{
    public NetEntity Requester { get; set; }
    public List<NetEntity> Entities { get; set; } = new();
    public List<NetEntity> Targets { get; set; } = new();
    public NetCoordinates TargetCoordinates { get; set; }
}
