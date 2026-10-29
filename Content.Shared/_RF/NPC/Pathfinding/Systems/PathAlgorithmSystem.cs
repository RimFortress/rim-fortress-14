using System.Threading.Tasks;
using Content.Shared._RF.NPC.Pathfinding.Algorithms;

namespace Content.Shared._RF.NPC.Pathfinding.Systems;

/// <summary>
/// An entity system that implements a path search algorithm.
/// </summary>
/// <typeparam name="T">Path algorithm type.</typeparam>
public abstract partial class PathAlgorithmSystem<T> : EntitySystem where T : BasePathAlgorithm<T>
{
    [Dependency] protected SharedRfPathfindingSystem Pathfinding = null!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PathAlgorithmSearch<T>>(OnSearch);
    }

    private void OnSearch(ref PathAlgorithmSearch<T> ev)
    {
        ev.Result = Search(ev.Search, ev.Algorithm);
    }

    /// <summary>
    /// Searches for a path.
    /// </summary>
    /// <remarks>
    /// The cost of a node is calculated by <see cref="SharedRfPathfindingSystem.EvaluateNode"/>. The method must call
    /// <see cref="PathJob.Yield"/> regularly, and must stop when <see cref="PathJob.Token"/> is cancelled.
    /// </remarks>
    /// <param name="search">The input of the search.</param>
    /// <param name="algorithm">The algorithm and its settings.</param>
    protected abstract Task<PathSearchResult> Search(PathSearch search, T algorithm);
}
