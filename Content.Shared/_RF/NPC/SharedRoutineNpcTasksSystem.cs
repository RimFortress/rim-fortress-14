using Robust.Shared.Serialization;

namespace Content.Shared._RF.NPC;

public abstract class SharedRoutineNpcTasksSystem : EntitySystem
{
}

public sealed class NpcJobData(int id, string name, string? iconPath, List<string> tasks, bool preset)
{
    public int Id { get; } = id;
    public string Name { get; } = name;
    public string? IconPath { get; } = iconPath;
    public List<string> Tasks { get; } =  tasks;
    public bool Preset { get; } = preset;
}

[Serializable, NetSerializable]
public sealed class NpcJobsInfoRequest : EntityEventArgs
{
}

[Serializable, NetSerializable]
public sealed class NpcJobInfoMessage(NpcJobData job) : EntityEventArgs
{
    public NpcJobData Job { get; } = job;
}

[Serializable, NetSerializable]
public sealed class NpcJobCreateRequest(NpcJobData job) : EntityEventArgs
{
    public NpcJobData Job { get; } = job;
}

[Serializable, NetSerializable]
public sealed class NpcJobDeleted(int id) : EntityEventArgs
{
    public int Id { get; } = id;
}

[Serializable, NetSerializable]
public sealed class NpcJobPriority(int id, NetEntity entity, int priority) : EntityEventArgs
{
    public int Id { get; } = id;
    public NetEntity Entity { get; } = entity;
    public int Priority { get; } = priority;
}

[Serializable, NetSerializable]
public sealed class NpcJobUpdated(int id, string? name, string? iconPath, List<string>? tasks) : EntityEventArgs
{
    public int Id { get; } = id;
    public string? Name { get; } = name;
    public string? IconPath { get; } = iconPath;
    public List<string>? Tasks { get; } = tasks;
}
