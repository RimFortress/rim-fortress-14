using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._RF.NPC;

public abstract class SharedNpcControlSystem : EntitySystem
{

}

public sealed class NpcTask
{
    public Color Color { get; }
    public EntityUid? Target { get; }
    public EntityCoordinates? Coordinates { get; }

    public NpcTask(Color color, EntityUid? target, EntityCoordinates? coords)
    {
        Color = color;
        Target = target;
        Coordinates = coords;
    }
}

[Serializable, NetSerializable]
public sealed class NpcTaskInfoMessage : EntityEventArgs
{
    public NetEntity Entity { get; set; }
    public Color Color { get; set; }
    public NetEntity? Target { get; set; }
    public NetCoordinates? TargetCoordinates { get; set; }
}

[Serializable, NetSerializable]
public sealed class NpcTaskResetMessage : EntityEventArgs
{
    public NetEntity Entity { get; set; }
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
    public NetEntity? Target { get; set; } = new();
    public NetCoordinates TargetCoordinates { get; set; }
}
