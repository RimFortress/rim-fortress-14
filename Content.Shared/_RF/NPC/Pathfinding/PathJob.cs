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
    CancellationToken cancellation = default)
    : Job<PathSearchResult>(maxTime, cancellation)
{
    public CancellationToken Token => Cancellation;

    /// <summary>
    /// Gives control back to the job queue if the job has run out of its time slice.
    /// Algorithms must call it regularly, but not necessarily on every step.
    /// </summary>
    public ValueTask Yield() => SuspendIfOutOfTime();

    protected override async Task<PathSearchResult> Process()
    {
        return await algorithm.Search(new PathSearch(agent, start, target, range, this), runner);
    }
}
