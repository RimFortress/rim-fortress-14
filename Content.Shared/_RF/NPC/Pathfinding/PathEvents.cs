using System.Threading.Tasks;
using Content.Shared._RF.NPC.Pathfinding.Algorithms;
using JetBrains.Annotations;

namespace Content.Shared._RF.NPC.Pathfinding;

/// <summary>
/// An event raised to run a <see cref="PathAlgorithm"/>. The handler starts the search and sets the result,
/// which finishes when the search is done.
/// </summary>
[PublicAPI, ByRefEvent]
public record struct PathAlgorithmSearch<T>(PathSearch Search, T Algorithm, Task<PathSearchResult>? Result)
    where T : BasePathAlgorithm<T>;

/// <summary>
/// An event raised to check a <see cref="PathCondition"/>.
/// </summary>
[PublicAPI, ByRefEvent]
public record struct PathConditionCheck<T>(PathNodeContext Node, T Condition, bool Result)
    where T : BasePathCondition<T>;
