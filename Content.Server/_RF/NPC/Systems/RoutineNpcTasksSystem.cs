using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server._RF.NPC.Components;
using Content.Server._RF.NPC.Prototypes;
using Content.Shared._RF.NPC;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._RF.NPC.Systems;

/// <summary>
/// Manages <see cref="RoutineNpcTasksComponent"/>
/// </summary>
public sealed class RoutineNpcTasksSystem : SharedRoutineNpcTasksSystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly NpcControlSystem _control = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly List<NpcJob> _jobs = new();
    private int _nextJobId = 1;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoutineNpcTasksComponent, NpcTaskFinished>(OnTaskFinished);
        SubscribeLocalEvent<RoutineNpcTasksComponent, NpcControllerAdded>(OnControllerAdded);

        SubscribeNetworkEvent<NpcJobsInfoRequest>(OnInfoRequest);
        SubscribeNetworkEvent<NpcJobDeleted>(OnJobDeleted);
        SubscribeNetworkEvent<NpcJobPriority>(OnJobPriority);
        SubscribeNetworkEvent<NpcJobUpdated>(OnJobUpdated);
        SubscribeNetworkEvent<NpcJobCreateRequest>(OnJobCreateRequest);

        _prototype.PrototypesReloaded += args =>
        {
            if (args.WasModified<NpcJobPrototype>())
                ReloadPrototypes();
        };

        ReloadPrototypes();
    }

    #region Events Handle

    private void ReloadPrototypes()
    {
        foreach (var proto in _prototype.EnumeratePrototypes<NpcJobPrototype>())
        {
            var index = _jobs.FindIndex(x => x.Proto == proto);

            if (index != -1)
            {
                var job = new NpcJob(_jobs[index].Id, proto);
                _jobs[index] = job;
                RaiseNetworkEvent(InfoMessage(job));
            }
            else
            {
                var job = new NpcJob(_nextJobId, proto);

                _nextJobId++;
                _jobs.Add(job);

                if (!job.Hidden)
                    RaiseNetworkEvent(InfoMessage(job));
            }
        }

        foreach (var job in _jobs)
        {
            if (job.Proto != null && !_prototype.HasIndex(job.Proto))
                DeleteJob(job.Id);
        }
    }

    private void OnTaskFinished(EntityUid uid, RoutineNpcTasksComponent comp, NpcTaskFinished args)
    {
        if (!TryGetCurrentJob(uid, out var job)
            || job.Tasks.FirstOrNull(x => x == args.Task) == null
            || !_prototype.TryIndex(args.Task, out var proto))
            return;

        if (args.Failed && job.CooldownOnFail != null)
            comp.AvailableOn[job.Id] = _timing.CurTime + job.CooldownOnFail.Value;

        if (job.FinishOnFailed && args.Failed)
            return;

        _control.TrySetPassiveTask(uid, proto);
    }

    private void OnControllerAdded(EntityUid uid, RoutineNpcTasksComponent comp, NpcControllerAdded args)
    {
        foreach (var proto in comp.PresetJobs)
        {
            if (!TryGetJob(proto, out var job) || !_prototype.TryIndex(proto, out var protoJob))
                continue;

            SetJobPriority(uid, job.Id, protoJob.DefaultPriority);
        }
    }

    private void OnInfoRequest(NpcJobsInfoRequest msg, EntitySessionEventArgs args)
    {
        var tasks = new HashSet<NpcTaskData>();
        var jobs = _jobs
            .Where(x => (x.Owner == null || x.Owner == args.SenderSession) && !x.Hidden);

        foreach (var job in jobs)
        {
            RaiseNetworkEvent(InfoMessage(job), args.SenderSession);

            foreach (var task in job.Tasks)
            {
                if (!_prototype.TryIndex(task, out var proto))
                    continue;

                var data = new NpcTaskData(proto.ID, proto.Name, proto.Description, proto.OverlayColor);
                tasks.Add(data);
            }
        }

        RaiseNetworkEvent(new NpcJobsTasksInfoMessage(tasks));
    }

    private void OnJobDeleted(NpcJobDeleted msg, EntitySessionEventArgs args)
    {
        if (!TryGetJob(msg.Id, out var job) || job.Owner != args.SenderSession)
            return;

        DeleteJob(job.Id);
    }

    private void OnJobPriority(NpcJobPriority msg, EntitySessionEventArgs args)
    {
        var uid = GetEntity(msg.Entity);

        if (!_control.CanControl(args.SenderSession, uid) || !TryGetJob(msg.Id, out var job))
            return;

        if (job.Owner != null && job.Owner != args.SenderSession)
            return;

        SetJobPriority(uid, msg.Id, msg.Priority);
    }

    private void OnJobUpdated(NpcJobUpdated msg, EntitySessionEventArgs args)
    {
        if (!TryGetJob(msg.Id, out var job) || job.Owner != args.SenderSession)
            return;

        if (msg.Name != null)
            job.Name = msg.Name;

        if (msg.IconPath != null)
            job.Icon = new SpriteSpecifier.Texture(new(msg.IconPath));

        if (msg.Tasks != null)
        {
            job.Tasks.Clear();

            foreach (var task in msg.Tasks)
            {
                if (!_prototype.TryIndex<NpcTaskPrototype>(task, out var proto)
                    || !AccessibleTask(args.SenderSession, proto))
                    continue;

                job.Tasks.Add(proto);
            }
        }

        RaiseNetworkEvent(InfoMessage(job), args.SenderSession);
    }

    public void OnJobCreateRequest(NpcJobCreateRequest msg, EntitySessionEventArgs args)
    {
        var tasks = new List<ProtoId<NpcTaskPrototype>>();

        foreach (var task in msg.Job.Tasks)
        {
            if (!_prototype.TryIndex<NpcTaskPrototype>(task, out var proto)
                || !AccessibleTask(args.SenderSession, proto))
                continue;

            tasks.Add(proto);
        }

        var icon = msg.Job.IconPath is { } path ? new SpriteSpecifier.Texture(new(path)) : null;
        CreateJob(msg.Job.Name, args.SenderSession, icon, tasks);
    }

    #endregion

    /// <summary>
    /// Sets the priority of the job position when selecting. 1 is highest
    /// </summary>
    public void SetJobPriority(Entity<RoutineNpcTasksComponent?, ControllableNpcComponent?> entity, int jobId, int priority)
    {
        if (!Resolve(entity, ref entity.Comp1) || !Resolve(entity, ref entity.Comp2))
            return;

        priority = Math.Clamp(priority, 0, entity.Comp1.MaxPriority);

        foreach (var uid in entity.Comp2.CanControl)
        {
            RaiseNetworkEvent(new NpcJobPriority(jobId, GetNetEntity(entity), priority), uid);
        }

        if (priority == 0)
        {
            entity.Comp1.Jobs.Remove(jobId);
            return;
        }

        entity.Comp1.Jobs[jobId] = priority;
    }

    /// <summary>
    /// Attempts to give NPCs the first suitable routine task
    /// </summary>
    public bool TrySetRoutineTask(Entity<RoutineNpcTasksComponent?> entity)
    {
        if (!Resolve(entity, ref entity.Comp))
            return false;

        foreach (var job in OrderedJobs(entity))
        {
            foreach (var protoId in job.Tasks)
            {
                if (!_prototype.TryIndex(protoId, out var task)
                    || !_control.TrySetPassiveTask(entity.Owner, task))
                    continue;

                entity.Comp.AvailableOn.Remove(job.Id);
                entity.Comp.CurrentJobId = job.Id;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Creates a custom NPC role
    /// </summary>
    /// <param name="name">role name displayed in the interface</param>
    /// <param name="owner">player who created this role</param>
    /// <param name="icon">role icon displayed in the interface</param>
    /// <param name="tasks">the tasks assigned to the job</param>
    public void CreateJob(
        string name,
        ICommonSession owner,
        SpriteSpecifier.Texture? icon = null,
        List<ProtoId<NpcTaskPrototype>>? tasks = null)
    {
        var job = new NpcJob(_nextJobId, name, owner, icon, tasks);

        _nextJobId++;
        _jobs.Add(job);

        RaiseNetworkEvent(InfoMessage(job), owner);
    }

    /// <summary>
    /// Deletes a custom NPC role
    /// </summary>
    public bool DeleteJob(int id)
    {
        if (!TryGetJob(id, out var job) || job.Owner == null)
            return false;

        _jobs.Remove(job);
        RaiseNetworkEvent(new NpcJobDeleted(id), job.Owner);

        var query = EntityQueryEnumerator<RoutineNpcTasksComponent>();
        while (query.MoveNext(out var comp))
        {
            comp.Jobs.Remove(id);
        }

        return true;
    }

    public bool TryGetJob(int id, [NotNullWhen(true)] out NpcJob? job)
    {
        job = _jobs.Find(x => x.Id == id);
        return job != null;
    }

    public bool TryGetJob(ProtoId<NpcJobPrototype> protoId, [NotNullWhen(true)] out NpcJob? job)
    {
        job = _jobs.Find(x => x.Proto == protoId);
        return job != null;
    }

    public bool TryGetCurrentJob(Entity<RoutineNpcTasksComponent?> ent, [NotNullWhen(true)] out NpcJob? job)
    {
        job = null;

        if (!Resolve(ent, ref ent.Comp))
            return false;

        TryGetJob(ent.Comp.CurrentJobId, out job);
        return job != null;
    }

    /// <summary>
    /// Returns true if player has access to issue this task
    /// </summary>
    public bool AccessibleTask(ICommonSession user, ProtoId<NpcTaskPrototype> task)
    {
        return _jobs.Find(x =>
            (x.Owner == null || x.Owner == user)
            && !x.Hidden
            && x.Tasks.Contains(task)) != null;
    }

    private List<NpcJob> OrderedJobs(Entity<RoutineNpcTasksComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp))
            return new();

        var jobs = new Dictionary<NpcJob, int>();

        foreach (var (id, priority) in ent.Comp.Jobs)
        {
            if (priority == 0 || !TryGetJob(id, out var job) || job.Tasks.Count == 0)
                continue;

            if (ent.Comp.AvailableOn.TryGetValue(id, out var time) && _timing.CurTime < time)
                continue;

            jobs[job] = priority;
        }

        return jobs.OrderBy(x => x.Value).Select(x => x.Key).ToList();
    }

    private static NpcJobInfoMessage InfoMessage(NpcJob job)
    {
        return new NpcJobInfoMessage(new NpcJobData(
            job.Id,
            job.Name,
            job.Icon?.TexturePath.CanonPath,
            job.Tasks.Select(x => x.Id).ToList(),
            job.Proto != null));
    }
}

[Access(typeof(RoutineNpcTasksSystem))]
public sealed class NpcJob
{
    /// <summary>
    /// The unique id of this job
    /// </summary>
    [ViewVariables]
    public int Id { get; }

    [ViewVariables]
    public ICommonSession? Owner { get; }

    /// <summary>
    /// The name of this job
    /// </summary>
    [ViewVariables]
    public string Name;

    /// <summary>
    /// Stop the execution of a routine task if at least one active target of this task has failed to complete
    /// </summary>
    [ViewVariables]
    public bool FinishOnFailed = true;

    /// <summary>
    /// The time that this task cannot be called after a failed completion
    /// </summary>
    [ViewVariables]
    public TimeSpan? CooldownOnFail = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The icon for this job.
    /// </summary>
    [ViewVariables]
    public SpriteSpecifier.Texture? Icon;

    /// <summary>
    /// Tasks included in this job
    /// </summary>
    [ViewVariables]
    public List<ProtoId<NpcTaskPrototype>> Tasks;

    /// <summary>
    /// The id of the prototype of the preset job from which this class is created
    /// </summary>
    [ViewVariables]
    public ProtoId<NpcJobPrototype>? Proto;

    [ViewVariables]
    public bool Hidden { get; }

    public NpcJob(int id,
        string name,
        ICommonSession owner,
        SpriteSpecifier.Texture? icon = null,
        List<ProtoId<NpcTaskPrototype>>? tasks = null)
    {
        Id = id;
        Name = name;
        Owner = owner;
        Tasks = tasks ?? new();
        Icon = icon;
    }

    public NpcJob(int id, NpcJobPrototype proto)
    {
        Id = id;
        Name = proto.Name;
        Owner = null;
        Tasks = proto.Tasks;
        Icon = proto.Icon;
        Proto = proto;
        Hidden = proto.Hidden;
    }
}
