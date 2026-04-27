using Content.Shared._RF.NPC.GOAP;
using JetBrains.Annotations;

namespace Content.Shared._RF.NPC.Search;

/// <summary>
/// An event raised to get the result of a search query.
/// </summary>
/// <typeparam name="T">Search query type.</typeparam>
/// <param name="Query">Search query.</param>
/// <param name="State">GoapState of the agent requesting the search.</param>
/// <param name="Result">Search query result.</param>
[PublicAPI, ByRefEvent]
public record struct GetSearchQuery<T>(T Query, GoapState State, HashSet<EntityUid> Result) where T : BaseSearchQuery<T>;

/// <summary>
/// An event raised to filter the target entity from a search query.
/// </summary>
/// <typeparam name="T"><Search filter type./typeparam>
/// <param name="Filter">Search filter.</param>
/// <param name="State">GoapState of the agent requesting the search.</param>
/// <param name="Target">Target entity.</param>
/// <param name="Result">Filter result.</param>
[PublicAPI, ByRefEvent]
public record struct GetSearchFilter<T>(T Filter, GoapState State, EntityUid Target, bool Result) where T : BaseSearchFilter<T>;

/// <summary>
/// An event raised to get the result of the consideration of the target entity in the search query.
/// </summary>
/// <typeparam name="T">Сonsideration type.</typeparam>
/// <param name="Con">Search consideration.</param>
/// <param name="State">GoapState of the agent requesting the search.</param>
/// <param name="Target">Target entity.</param>
/// <param name="Result">Result score.</param>
[PublicAPI, ByRefEvent]
public record struct GetSearchScore<T>(T Con, GoapState State, EntityUid Target, float Result) where T : BaseSearchConsideration<T>;
