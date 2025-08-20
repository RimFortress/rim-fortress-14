using Content.Shared._RF.NPC;

namespace Content.Client._RF.NPC;

public sealed class RoutineNpcTasksSystem : SharedRoutineNpcTasksSystem
{
    public readonly Dictionary<string, NpcTaskData> TasksData = new();
    public readonly Dictionary<int, NpcJobData> JobsData = new();

    public event Action<int>? OnJobChanged;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<NpcJobPriority>(OnJobPriority);
        SubscribeNetworkEvent<NpcJobInfoMessage>(OnJobInfo);
        SubscribeNetworkEvent<NpcJobDeleted>(OnJobDeleted);
        SubscribeNetworkEvent<NpcJobsTasksInfoMessage>(OnJobsTasksInfo);
    }

    private void OnJobPriority(NpcJobPriority msg)
    {
        var uid = GetEntity(msg.Entity);
        var comp = EnsureComp<RoutineNpcTasksComponent>(uid);

        comp.Jobs[msg.Id] = msg.Priority;
    }

    private void OnJobInfo(NpcJobInfoMessage msg)
    {
        JobsData[msg.Job.Id] = msg.Job;
        OnJobChanged?.Invoke(msg.Job.Id);
    }

    private void OnJobDeleted(NpcJobDeleted msg)
    {
        JobsData.Remove(msg.Id);
        OnJobChanged?.Invoke(msg.Id);
    }

    private void OnJobsTasksInfo(NpcJobsTasksInfoMessage msg)
    {
        TasksData.Clear();

        foreach (var task in msg.Tasks)
        {
            TasksData[task.TaskId] = task;
        }
    }

    public void UpdateJob(int jobId, string? name = null, string? iconPath = null, List<string>? tasks = null)
    {
        if (!JobsData.ContainsKey(jobId))
            return;

        if (name == null && iconPath == null && tasks == null)
            return;

        RaiseNetworkEvent(new NpcJobUpdated(jobId, name, iconPath, tasks));
    }

    public void ChangeJobPriority(EntityUid uid, int jobId, int priority)
    {
        if (!TryComp(uid, out RoutineNpcTasksComponent? _) || !JobsData.ContainsKey(jobId))
            return;

        RaiseNetworkEvent(new NpcJobPriority(jobId, GetNetEntity(uid), priority));
    }

    public void CreateJob(string name, List<string> tasks, string? iconPath = null)
    {
        var job = new NpcJobData(0, name, iconPath, tasks, false);
        RaiseNetworkEvent(new NpcJobCreateRequest(job));
    }

    public void DeleteJob(int jobId)
    {
        if (!JobsData.ContainsKey(jobId))
            return;

        RaiseNetworkEvent(new NpcJobDeleted(jobId));
    }

    public int GetPriority(EntityUid uid, int jobId)
    {
        if (!TryComp(uid, out RoutineNpcTasksComponent? comp) || !comp.Jobs.TryGetValue(jobId, out var priority))
            return 0;

        return priority;
    }
}
