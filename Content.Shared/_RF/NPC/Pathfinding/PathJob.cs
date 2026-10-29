using JetBrains.Annotations;
using System.Threading;
using System.Threading.Tasks;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.Pathfinding.Algorithms;
using Content.Shared._RF.NPC.Pathfinding.Components;
using Robust.Shared.CPUJob.JobQueues;
using Robust.Shared.Map;

namespace Content.Shared._RF.NPC.Pathfinding;

/// <summary>
/// Runs a path search algorithm in a time-sliced job, so a long search does not block the game.
/// </summary>
public sealed class PathJob(
    double maxTime,
    Entity<PathProfilesComponent, GoapComponent> agent,
    EntityCoordinates start,
    EntityCoordinates? target,
    float range,
    PathAlgorithm algorithm,
    IPathAlgorithmRunner runner,
    CancellationToken cancellation = default,
    Box2? targetBounds = null,
    IReadOnlyList<PathAvoid>? avoid = null)
    : Job<PathSearchResult>(maxTime, cancellation)
{
    /// <summary>
    /// Cancels the search when the request is no longer needed.
    /// </summary>
    public CancellationToken Token => Cancellation;

    /// <summary>
    /// Gives control back to the job queue if the job has run out of its time slice.
    /// Algorithms must call it regularly, but not necessarily on every step.
    /// </summary>
    [PublicAPI]
    public ValueTask Yield() => SuspendIfOutOfTime();

    protected override async Task<PathSearchResult> Process()
    {
        // The request may have been cancelled while the job was waiting in the queue.
        if (Cancellation.IsCancellationRequested)
            return PathSearchResult.NoPath;

        return await algorithm.Search(new PathSearch(agent, start, target, range, this, targetBounds, avoid), runner);
    }
}
