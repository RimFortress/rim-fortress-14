using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._RF.NPC;

public abstract class SharedNpcControlSystem : EntitySystem
{

}

public sealed class NpcTask(NpcTaskInfoMessage msg, IEntityManager manager)
{
    public string TaskId { get; } = msg.TaskId;
    public string TaskName { get; } = msg.TaskName;
    public string? Description { get; } = msg.Description;
    public string? IconPath { get; } = msg.IconPath;
    public Color Color { get; } = msg.Color;
    public EntityUid? Target { get; } = manager.GetEntity(msg.Target);
    public EntityCoordinates? Coordinates { get; } = manager.GetCoordinates(msg.TargetCoordinates);
}

[Serializable, NetSerializable]
public sealed class NpcTaskInfoMessage(
    string taskId,
    string taskName,
    string? description,
    string? iconPath,
    Color color,
    NetEntity entity,
    NetEntity? target,
    NetCoordinates? targetCoordinates) : EntityEventArgs
{
    public string TaskId { get; set; } = taskId;
    public string TaskName { get; set; } = taskName;
    public string? Description { get; set; } = description;
    public string? IconPath { get; set; } = iconPath;
    public Color Color { get; set; } = color;
    public NetEntity Entity { get; set; } = entity;
    public NetEntity? Target { get; set; } = target;
    public NetCoordinates? TargetCoordinates { get; set; } = targetCoordinates;
}

[Serializable, NetSerializable]
public sealed class NpcTaskFinishMessage(string taskId, NetEntity entity) : EntityEventArgs
{
    public string TaskId { get; set; } = taskId;
    public NetEntity Entity { get; } = entity;
}

[Serializable, NetSerializable]
public sealed class NpcTaskRequest : EntityEventArgs
{
    public NetEntity Requester { get; set; }
    public List<NetEntity> Entities { get; set; } = new();
    public NetEntity? Target { get; set; } = new();
    public NetCoordinates TargetCoordinates { get; set; }
}

[Serializable, NetSerializable]
public sealed class NpcTasksContextMenuMessage : EntityEventArgs
{

}

[Serializable, NetSerializable]
public sealed class PassiveNpcTaskRequest(
    NetEntity requester,
    string taskId,
    List<NetEntity> entities) : EntityEventArgs
{
    public NetEntity Requester { get; set; } = requester;
    public string TaskId { get; set; } = taskId;
    public List<NetEntity> Entities { get; set; } = entities;
}

[Serializable, NetSerializable]
public sealed class PassiveNpcTaskMessage(string taskId, List<NetEntity> entities) : EntityEventArgs
{
    public string TaskId { get; set; } = taskId;
    public List<NetEntity> Entities { get; set; } = entities;
}

[Serializable, NetSerializable]
public sealed class PassiveNpcTaskRemoveRequest(NetEntity requester, List<NetEntity> entities) : EntityEventArgs
{
    public NetEntity Requester { get; set; } = requester;
    public List<NetEntity> Entities { get; set; } = entities;
}

[Serializable, NetSerializable]
public sealed class PassiveNpcTaskRemoveMessage(List<NetEntity> entities) : EntityEventArgs
{
    public List<NetEntity> Entities { get; set; } = entities;
}

[Serializable, NetSerializable]
public sealed class AllowedNpcTasksInfoRequest(NetEntity requester) : EntityEventArgs
{
    public NetEntity Requester { get; set; } = requester;
}

[Serializable, NetSerializable]
public sealed class AllowedNpcTasksInfoMessage(List<NpcTaskInfoMessage> info) : EntityEventArgs
{
    public List<NpcTaskInfoMessage> Info { get; set; } = info;
}
