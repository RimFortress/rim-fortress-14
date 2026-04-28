using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared._RF.NPC.Components;
using Content.Shared._RF.NPC.UtilityAi;
using Content.Shared._RF.NPC.UtilityAi.Prototypes;
using Content.Shared._RF.NPC.UtilityAi.Systems;
using JetBrains.Annotations;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._RF.NPC.Systems;

public sealed class NpcJobsSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly OwnershipSystem _ownership = default!;

    private readonly Dictionary<int, EntityUid> _jobOwners = new();

    private int _nextJobId = 1;

    /// <summary>
    /// An event that notifies of a change in an NPC's jobs. Invoked only on the client.
    /// </summary>
    public Action<int>? OnJobChanged;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NpcJobsComponent, ComponentInit>(OnNpcJobsInit);
        SubscribeLocalEvent<NpcJobSettingsComponent, NpcControllerAdded>(OnControllerAdded);
        SubscribeLocalEvent<NpcJobSettingsComponent, UtilityAiGoalScoreModify>(OnScoreModify);

        SubscribeNetworkEvent<NpcJobCreated>(OnJobCreated);
        SubscribeNetworkEvent<NpcJobDeleted>(OnJobDeleted);
        SubscribeNetworkEvent<NpcJobPriority>(OnJobPriority);
        SubscribeNetworkEvent<NpcJobUpdated>(OnJobUpdated);
    }

    #region Events Handle

    private void OnNpcJobsInit(Entity<NpcJobsComponent> ent, ref ComponentInit args)
    {
        foreach (var job in ent.Comp.Jobs)
        {
            job.Id = _nextJobId;
            job.Name = Loc.GetString(job.Name);
            _jobOwners[_nextJobId] = ent;
            _nextJobId++;

            foreach (var goal in job.Goals)
            {
                ent.Comp.AccessibleGoals.Add(goal);
            }
        }
    }

    private void OnControllerAdded(Entity<NpcJobSettingsComponent> ent, ref NpcControllerAdded args)
    {
        if (!TryComp(args.User, out NpcJobsComponent? comp))
            return;

        foreach (var job in comp.Jobs)
        {
            SetJobPriority(ent.Owner, job.Id, comp.MaxPriority);
        }
    }

    private void OnScoreModify(Entity<NpcJobSettingsComponent> ent, ref UtilityAiGoalScoreModify ev)
    {
        foreach (var (jobId, priority) in ent.Comp.Jobs)
        {
            if (!TryGetJob(jobId, out var job) || !job.Goals.Contains(ev.Goal))
                continue;

            ev.Score *= 1 / priority;
            return;
        }
    }

    private void OnJobDeleted(NpcJobDeleted msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity == null)
            return;

        DeleteJob(args.SenderSession.AttachedEntity.Value, msg.Id);
    }

    private void OnJobPriority(NpcJobPriority msg, EntitySessionEventArgs args)
    {
        if (!TryComp(args.SenderSession.AttachedEntity, out NpcJobsComponent? comp)
            || !IsOwner(msg.Id, args.SenderSession.AttachedEntity))
            return;

        SetJobPriority(GetEntity(msg.Entity), msg.Id, msg.Priority);
    }

    private void OnJobUpdated(NpcJobUpdated msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity == null)
            return;

        UpdateJob(
            args.SenderSession.AttachedEntity.Value,
            msg.Job.Id,
            msg.Job.Name,
            msg.Job.Icon,
            msg.Job.Goals);
    }

    private void OnJobCreated(NpcJobCreated msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity == null)
            return;

        CreateJob(args.SenderSession.AttachedEntity.Value, msg.Job.Name, msg.Job.Icon, msg.Job.Goals);
    }

    #endregion

    /// <summary>
    /// Sets the NPC's job priority.
    /// </summary>
    /// <remarks>
    /// The priority of a job affects Utility AI's score for the goals associated with that job,
    /// calculated using the formula: score * (1 / priority).
    /// </remarks>
    /// <param name="ent">The entity for which the priority needs to be changed.</param>
    /// <param name="jobId">NPC job id.</param>
    /// <param name="priority">Priority.</param>
    [PublicAPI]
    public void SetJobPriority(Entity<NpcJobSettingsComponent?> ent, int jobId, int priority)
    {
        if (!Resolve(ent, ref ent.Comp)
            || !_jobOwners.TryGetValue(jobId, out var owner)
            || !TryComp(owner, out NpcJobsComponent? comp))
            return;

        priority = Math.Clamp(priority, 0, comp.MaxPriority);
        ent.Comp.Jobs[jobId] = priority;
        Dirty(ent);

        if (!_net.IsClient)
            return;

        foreach (var uid in _ownership.GetOwners(ent))
        {
            RaiseNetworkEvent(new NpcJobPriority(jobId, GetNetEntity(ent), priority), uid);
        }
    }

    /// <summary>
    /// Creates a custom NPC job.
    /// </summary>
    /// <param name="ent">Player who created this job.</param>
    /// <param name="name">Job name displayed in the interface.</param>
    /// <param name="icon">Role icon displayed in the interface.</param>
    /// <param name="goals">The goals assigned to the job.</param>
    [PublicAPI]
    public void CreateJob(
        Entity<NpcJobsComponent?> ent,
        string name,
        SpriteSpecifier? icon = null,
        List<ProtoId<UtilityAiGoalPrototype>>? goals = null)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        var job = new NpcJob
        {
            Id = _nextJobId,
            Icon = icon,
            Name = name,
            Goals = goals ?? new(),
        };

        _jobOwners[_nextJobId] = ent;
        _nextJobId++;
        ent.Comp.Jobs.Add(job);
        Dirty(ent);

        if (_net.IsClient)
        {
            RaiseNetworkEvent(new NpcJobCreated(job), ent);
            OnJobChanged?.Invoke(job.Id);
            return;
        }

        foreach (var uid in _ownership.GetOwned(ent.Owner))
        {
            if (TryComp(uid, out NpcJobSettingsComponent? comp))
                SetJobPriority(new(uid, comp), job.Id, ent.Comp.MaxPriority);
        }
    }

    /// <summary>
    /// Updates the NPC job settings.
    /// </summary>
    /// <param name="ent">Creator of this job.</param>
    /// <param name="jobId">Job ID.</param>
    /// <param name="name">New job name.</param>
    /// <param name="icon">New job icon.</param>
    /// <param name="goals">New job goals.</param>
    [PublicAPI]
    public void UpdateJob(
        Entity<NpcJobsComponent?> ent,
        int jobId,
        string name,
        SpriteSpecifier? icon,
        List<ProtoId<UtilityAiGoalPrototype>> goals)
    {
        if (!Resolve(ent, ref ent.Comp)
            || !IsOwner(jobId, ent)
            || ent.Comp.Jobs.FirstOrDefault(x => x.Id == jobId) is not { } job)
            return;

        job.Name = name;
        job.Icon = icon;
        job.Goals = goals.Where(ent.Comp.AccessibleGoals.Contains).ToList();
        Dirty(ent);

        if (_net.IsClient)
        {
            RaiseNetworkEvent(new NpcJobUpdated(job), ent);
            OnJobChanged?.Invoke(job.Id);
        }
    }

    /// <summary>
    /// Deletes a NPC job.
    /// </summary>
    [PublicAPI]
    public bool DeleteJob(Entity<NpcJobsComponent?> ent, int id)
    {
        if (!Resolve(ent, ref ent.Comp)
            || !TryGetJob(id, out var job)
            || !IsOwner(job, ent))
            return false;

        _jobOwners.Remove(id);
        ent.Comp.Jobs.Remove(job);
        Dirty(ent);

        var query = EntityQueryEnumerator<NpcJobSettingsComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            comp.Jobs.Remove(id);
            Dirty(uid, comp);
        }

        if (_net.IsClient)
        {
            RaiseNetworkEvent(new NpcJobDeleted(id), ent);
            OnJobChanged?.Invoke(job.Id);
        }

        return true;
    }

    /// <summary>
    /// Returns the NPC's job by its ID.
    /// </summary>
    [PublicAPI, Pure]
    public bool TryGetJob(int id, [NotNullWhen(true)] out NpcJob? job)
    {
        job = null;

        if (!_jobOwners.TryGetValue(id, out var owner)
            || !TryComp(owner, out NpcJobsComponent? comp))
            return false;

        job = comp.Jobs.FirstOrDefault(x => x.Id == id);
        return job != null;
    }

    /// <summary>
    /// Returns the priority of this job for the NPC.
    /// </summary>
    /// <param name="ent">NPC entity.</param>
    /// <param name="jobId">Job id.</param>
    [PublicAPI, Pure]
    public int GetPriority(Entity<NpcJobSettingsComponent?> ent, int jobId)
        => Resolve(ent, ref ent.Comp) && ent.Comp.Jobs.TryGetValue(jobId, out var priority) ? priority : 0;

    /// <summary>
    /// Returns true if the entity is the creator of this NPC job.
    /// </summary>
    [PublicAPI, Pure]
    public bool IsOwner(int jobId, EntityUid? uid)
        => _jobOwners.TryGetValue(jobId, out var owner) && owner == uid;

    /// <inheritdoc cref="IsOwner"/>
    [PublicAPI, Pure]
    public bool IsOwner(NpcJob job, EntityUid? uid) => IsOwner(job.Id, uid);
}

[Serializable, NetSerializable]
public sealed class NpcJobCreated(NpcJob job) : EntityEventArgs
{
    public NpcJob Job { get; } = job;
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
public sealed class NpcJobUpdated(NpcJob job) : EntityEventArgs
{
    public NpcJob Job { get; } = job;
}
