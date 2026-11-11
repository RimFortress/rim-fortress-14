using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Content.Shared._RF.NPC.Pathfinding.Algorithms;

/// <summary>
/// A path search algorithm and its settings, specified in a path profile.
/// </summary>
/// <remarks>
/// Like conditions, the data of an algorithm is separated from its implementation:
/// the implementation is a <see cref="Systems.PathAlgorithmSystem{T}"/>.
/// </remarks>
[ImplicitDataDefinitionForInheritors]
public abstract partial class PathAlgorithm
{
    /// <summary>
    /// Searches for a path. The search is asynchronous only in the sense of the job:
    /// it continues in the following ticks, whenever the algorithm yields through <see cref="PathSearch.Job"/>.
    /// </summary>
    [Pure]
    public abstract Task<PathSearchResult> Search(PathSearch search, IPathAlgorithmRunner runner);
}

/// <summary>
/// A <see cref="PathAlgorithm"/> that uses entity systems to work.
/// </summary>
public abstract partial class BasePathAlgorithm<T> : PathAlgorithm where T : BasePathAlgorithm<T>
{
    public override Task<PathSearchResult> Search(PathSearch search, IPathAlgorithmRunner runner)
        => runner.Search(search, (T)this);
}

/// <summary>
/// An object that is able to run <see cref="PathAlgorithm"/>s.
/// </summary>
public interface IPathAlgorithmRunner
{
    /// <summary>
    /// Runs the path search algorithm.
    /// </summary>
    /// <param name="search">The input of the search.</param>
    /// <param name="algorithm">The algorithm and its settings.</param>
    /// <returns>The result of the search, which is finished when the search is done.</returns>
    [PublicAPI]
    Task<PathSearchResult> Search<T>(PathSearch search, T algorithm) where T : BasePathAlgorithm<T>;
}
