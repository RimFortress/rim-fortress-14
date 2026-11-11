using Content.Shared._RF.NPC.Search.Prototypes;
using Robust.Shared.Prototypes;
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
public record struct GetSearchQuery<T>(T Query, GoapState State, HashSet<EntityUid> Result)
    where T : BaseSearchQuery<T>;

/// <summary>
/// An event raised to filter the target entity from a search query.
/// </summary>
/// <typeparam name="T">Search filter type.</typeparam>
/// <param name="Filter">Search filter.</param>
/// <param name="State">GoapState of the agent requesting the search.</param>
/// <param name="Target">Target entity.</param>
/// <param name="Result">Filter result.</param>
[PublicAPI, ByRefEvent]
public record struct GetSearchFilter<T>(T Filter, GoapState State, EntityUid Target, bool Result)
    where T : BaseSearchFilter<T>;

/// <summary>
/// An event raised to get the result of the consideration of the target entity in the search query.
/// </summary>
/// <typeparam name="T">Consideration type.</typeparam>
/// <param name="Con">Search consideration.</param>
/// <param name="State">GoapState of the agent requesting the search.</param>
/// <param name="Target">Target entity.</param>
/// <param name="Result">Result score.</param>
[PublicAPI, ByRefEvent]
public record struct GetSearchScore<T>(T Con, GoapState State, EntityUid Target, float Result)
    where T : BaseSearchConsideration<T>;

/// <summary>
/// An event raised when the AI marks a search result as temporarily captured.
/// </summary>
/// <param name="User">Entity that captured the result.</param>
[PublicAPI]
public readonly record struct SearchResultCaptured(EntityUid User);

/// <summary>
/// An event raised when the AI releases a temporarily captured search result.
/// </summary>
/// <param name="User">Entity that captured the result.</param>
[PublicAPI]
public readonly record struct SearchResultReleased(EntityUid User);

/// <summary>
/// An entity has got into the result of a search query of an agent, or has left it.
/// Raised on the agent only for the queries that have <see cref="Prototypes.SearchQueryPrototype.Notify"/>.
/// </summary>
/// <param name="Agent">The agent whose result has changed.</param>
/// <param name="Query">The search query.</param>
/// <param name="Entity">The entity.</param>
/// <param name="Added">True if it has got into the result, false if it has left it.</param>
[PublicAPI, ByRefEvent]
public readonly record struct SearchResultChangedEvent(
    EntityUid Agent,
    ProtoId<SearchQueryPrototype> Query,
    EntityUid Entity,
    bool Added);
