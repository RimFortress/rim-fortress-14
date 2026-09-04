using System.Diagnostics.CodeAnalysis;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search.Components;
using Content.Shared._RF.NPC.Search.Prototypes;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.NPC.Search.Systems;

/// <summary>
/// A system that handles search queries.
/// </summary>
/// <typeparam name="T">Search query type.</typeparam>
public abstract partial class NpcSearchQuerySystem<T> : EntitySystem where T : BaseSearchQuery<T>
{
    [Dependency] protected IPrototypeManager Proto = default!;
    [Dependency] protected SharedNpcSearcherSystem Searcher = default!;

    [Dependency] protected EntityQuery<NpcSearcherComponent> SearcherQuery = default!;
    [Dependency] protected EntityQuery<SearchTrackedComponent> TrackedQuery = default!;

    protected readonly HashSet<EntityUid> Query = new();
    private readonly Dictionary<ProtoId<SearchQueryPrototype>, T> _queries = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NpcSearcherComponent, GetSearchQuery<T>>(OnGetSearchQuery);

        Proto.PrototypesReloaded += args =>
        {
            if (args.WasModified<SearchQueryPrototype>())
                ReloadPrototypes();
        };

        ReloadPrototypes();
    }

    private void ReloadPrototypes()
    {
        _queries.Clear();

        foreach (var proto in Proto.EnumeratePrototypes<SearchQueryPrototype>())
        {
            if (proto.Query is T query)
                _queries[proto] = query;
        }
    }

    private void OnGetSearchQuery(Entity<NpcSearcherComponent> ent, ref GetSearchQuery<T> ev)
    {
        Query.Clear();
        GetQuery(ev.State, ev.Query);
        ev.Result = Query;
    }

    /// <summary>
    /// Saves the entities that match the search query in <see cref="Query"/>.
    /// </summary>
    /// <param name="state">GoapState of the agent requesting the search.</param>
    /// <param name="query">Search query.</param>
    protected abstract void GetQuery(GoapState state, T query);

    /// <summary>
    /// Returns an instance of a query of this type from this query prototype.
    /// </summary>
    /// <param name="protoId">Query prototype.</param>
    /// <param name="query">Query instance.</param>
    /// <typeparam name="T">Query type.</typeparam>
    /// <returns>True, if the query type in the prototype matches T</returns>
    [PublicAPI, Pure]
    public bool TryGetQuery(ProtoId<SearchQueryPrototype> protoId, [NotNullWhen(true)] out T? query)
        => _queries.TryGetValue(protoId, out query);

    /// <summary>
    /// Checks the query type of the search query prototype.
    /// </summary>
    /// <param name="protoId">Query prototype.</param>
    /// <typeparam name="T">Query type.</typeparam>
    [PublicAPI, Pure]
    public bool QueryTypeIs(ProtoId<SearchQueryPrototype> protoId) => _queries.ContainsKey(protoId);
}
