using System.Threading;
using System.Threading.Tasks;
using Robust.Shared.Map.Components;
using Content.Shared._RF.CCVar;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.Pathfinding;
using Content.Shared._RF.NPC.Pathfinding.Components;
using Content.Shared._RF.NPC.Pathfinding.Prototypes;
using JetBrains.Annotations;
using Robust.Shared.Configuration;
using Robust.Shared.CPUJob.JobQueues;
using Robust.Shared.CPUJob.JobQueues.Queues;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.Pathfinding.Systems;

// Path requests of the agents: the jobs that search for the paths and the queue that runs them.
public sealed partial class RfPathfindingSystem
{
    [Dependency] private IConfigurationManager _cfg = null!;

    // All of the searches are run by one queue, so together they never take more than their share of a tick.
    private readonly PathJobQueue _jobs = new();

    // How long a single search runs before it gives way to the others.
    private double _jobTime;

    /// <summary>
    /// A queue whose time per run can be changed.
    /// </summary>
    private sealed class PathJobQueue : JobQueue
    {
        public double Time = 0.004;

        public override double MaxTime => Time;
    }

    private void InitializeRequests()
    {
        _cfg.OnValueChanged(RfVars.PathfindingTime, value => _jobs.Time = value, true);
        _cfg.OnValueChanged(RfVars.PathfindingJobTime, value => _jobTime = value, true);
    }

    [SubscribeLocalEvent]
    private void OnProfilesShutdown(Entity<PathProfilesComponent> ent, ref ComponentShutdown args)
    {
        CancelPath(ent.AsNullable());
    }

    /// <summary>
    /// Starts a search for a path for the agent from where it is now. The profiles of the agent are tried in
    /// their order until one of them finds a path, and if none does, the best of what they found is the result.
    /// A request that is in progress is cancelled. Use <see cref="GetStatus"/> and <see cref="TryGetResult"/>
    /// to find out how the search went.
    /// </summary>
    /// <param name="agent">The agent that needs the path.</param>
    /// <param name="query">
    /// What the request is for. The algorithm of the query searches for the path, the rules of the profiles of the agent
    /// say what costs what, and the ones that are for other queries only are not used.
    /// </param>
    /// <param name="target">
    /// Where the path is to lead. If it is null, the algorithm of the query chooses the place itself, for example, a
    /// place to wander to, and then the range is how far from the agent the place can be.
    /// </param>
    /// <param name="range">How close to the target the agent needs to get.</param>
    /// <param name="profile">The only profile to try, null to try all the ones the agent can use.</param>
    /// <param name="targetBounds">
    /// The region the target takes up, in the coordinates of the entity of <paramref name="target"/>. The range is
    /// counted from it, so a target that is big and solid can be reached.
    /// </param>
    /// <param name="avoid">The places the path should not go by, such as the ones where the agent got stuck.</param>
    /// <returns>False if there is nothing to search with, no profile can be used.</returns>
    [PublicAPI]
    public bool RequestPath(
        Entity<PathProfilesComponent, GoapComponent> agent,
        ProtoId<PathQueryPrototype> query,
        EntityCoordinates? target,
        float range,
        ProtoId<PathProfilePrototype>? profile = null,
        Box2? targetBounds = null,
        IReadOnlyList<PathAvoid>? avoid = null)
    {
        CancelPath(new Entity<PathProfilesComponent?>(agent.Owner, agent.Comp1));

        var comp = agent.Comp1;

        if (!_proto.HasIndex(query))
            return false;

        if (profile is { } only)
        {
            if (!_proto.HasIndex(only))
                return false;

            comp.RequestProfiles.Add(only);
        }
        else
        {
            comp.RequestProfiles.AddRange(GetProfiles(agent));
        }

        comp.RequestQuery = query;
        comp.RequestTarget = target;
        comp.RequestRange = range;
        comp.RequestTargetBounds = targetBounds;
        comp.RequestAvoid = avoid == null ? null : new List<PathAvoid>(avoid);
        comp.RequestProfileIndex = -1;
        comp.RequestStatus = PathRequestStatus.Searching;

        if (StartNextProfile(agent))
            return true;

        CancelPath(new Entity<PathProfilesComponent?>(agent.Owner, agent.Comp1));
        return false;
    }

    /// <summary>
    /// Returns how the request of the agent is going. Call it to move the request on: when a search with a profile
    /// is over, the search with the next profile is started.
    /// </summary>
    /// <param name="agent">The agent.</param>
    [PublicAPI]
    public PathRequestStatus GetStatus(Entity<PathProfilesComponent, GoapComponent> agent)
    {
        var comp = agent.Comp1;

        while (comp.RequestStatus == PathRequestStatus.Searching
               && comp.Job is { Status: JobStatus.Finished } job)
        {
            var result = job.Result;

            if (job.Exception != null)
            {
                Log.Error($"Path search for {ToPrettyString(agent)} failed: {job.Exception}");
                result = PathSearchResult.NoPath;
            }

            comp.Job = null;
            comp.JobCancel?.Dispose();
            comp.JobCancel = null;

            // A cancelled or failed job has no result.
            if (result.Path == null)
                result = PathSearchResult.NoPath;

            var profile = comp.RequestProfiles[comp.RequestProfileIndex];

            // The first partial path is the best one, the profiles are in the order of preference.
            if (result.Status == PathSearchStatus.Path
                || result.Status == PathSearchStatus.PartialPath && comp.RequestResult.Status == PathSearchStatus.NoPath)
            {
                comp.RequestResult = result;
                comp.RequestProfile = profile;
            }

            if (result.Status == PathSearchStatus.Path || !StartNextProfile(agent))
                comp.RequestStatus = PathRequestStatus.Finished;
        }

        return comp.RequestStatus;
    }

    /// <summary>
    /// Takes the result of the request if the search is over. The result stays until the next request or
    /// <see cref="CancelPath"/>.
    /// </summary>
    /// <param name="agent">The agent.</param>
    /// <param name="result">The result, with <see cref="PathSearchStatus.NoPath"/> if no profile found a path.</param>
    /// <param name="profile">The profile that gave the result, null if there is no path.</param>
    /// <returns>True, if the search is over; otherwise, false.</returns>
    [PublicAPI]
    public bool TryGetResult(
        Entity<PathProfilesComponent, GoapComponent> agent,
        out PathSearchResult result,
        out ProtoId<PathProfilePrototype>? profile)
    {
        if (GetStatus(agent) != PathRequestStatus.Finished)
        {
            result = PathSearchResult.NoPath;
            profile = null;
            return false;
        }

        result = agent.Comp1.RequestResult;
        profile = agent.Comp1.RequestProfile;
        return true;
    }

    /// <summary>
    /// Cancels the request of the agent and forgets its result.
    /// </summary>
    /// <param name="agent">The agent.</param>
    [PublicAPI]
    public void CancelPath(Entity<PathProfilesComponent?> agent)
    {
        if (!Resolve(agent, ref agent.Comp, false))
            return;

        var comp = agent.Comp;

        // The job is left in the queue and finishes as soon as it notices.
        comp.JobCancel?.Cancel();
        comp.JobCancel?.Dispose();
        comp.JobCancel = null;
        comp.Job = null;

        comp.RequestProfiles.Clear();
        comp.RequestProfileIndex = 0;
        comp.RequestQuery = null;
        comp.RequestTarget = null;
        comp.RequestTargetBounds = null;
        comp.RequestAvoid = null;
        comp.RequestStatus = PathRequestStatus.None;
        comp.RequestResult = PathSearchResult.NoPath;
        comp.RequestProfile = null;
    }

    /// <summary>
    /// Builds the chunks of the grid that are waiting for the cooldown. A search that starts after the world has
    /// changed, for example after a wall was destroyed, must not see the world as it was.
    /// </summary>
    private void RebuildPending(EntityUid? grid)
    {
        if (grid is not { } uid
            || !TryComp(uid, out RfGridPathfindingComponent? map)
            || !TryComp(uid, out MapGridComponent? gridComp)
            || map.DirtyChunks.Count == 0)
            return;

        RebuildDirty(
            new Entity<RfGridPathfindingComponent, MapGridComponent>(uid, map, gridComp),
            new ParallelOptions { MaxDegreeOfParallelism = _parallel.ParallelProcessCount });
    }

    /// <summary>
    /// Starts the search with the next profile of the request that can be used.
    /// </summary>
    /// <returns>False if there are no profiles left.</returns>
    private bool StartNextProfile(Entity<PathProfilesComponent, GoapComponent> agent)
    {
        var comp = agent.Comp1;

        while (++comp.RequestProfileIndex < comp.RequestProfiles.Count)
        {
            var id = comp.RequestProfiles[comp.RequestProfileIndex];

            if (comp.RequestQuery is not { } queryId
                || !_proto.TryIndex(queryId, out var query)
                || !_proto.HasIndex(id)
                || !BeginRequest(agent, id, queryId))
            {
                continue;
            }

            var algorithm = query.Algorithm;

            RebuildPending(Transform(agent).GridUid);

            comp.JobCancel = new CancellationTokenSource();
            comp.Job = new PathJob(
                _jobTime,
                agent,
                Transform(agent).Coordinates,
                comp.RequestTarget,
                comp.RequestRange,
                algorithm,
                this,
                comp.JobCancel.Token,
                comp.RequestTargetBounds,
                comp.RequestAvoid);

            _jobs.EnqueueJob(comp.Job);
            return true;
        }

        return false;
    }
}
