using System.Diagnostics.CodeAnalysis;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Domains;
using Content.Shared._RF.NPC.Search.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.NPC.Search.Systems;

/// <summary>
/// The keys of the domain <c>Query/ProtoId/...</c>: the result of a search query of the agent, the best of it or all of it. The result
/// of a query changes as the searcher makes it again, see <see cref="SharedNpcSearcherSystem"/>, which tells
/// <see cref="GoapDomainSystem"/> about it, so what depends on the result of a query is up to date as well.
/// </summary>
public sealed partial class SearchGoapDomains : EntitySystem, IGoapDomainProvider
{
    public const string Family = "Query";

    [Dependency] private GoapDomainSystem _domains = null!;
    [Dependency] private SharedNpcSearcherSystem _searcher = null!;
    [Dependency] private IPrototypeManager _proto = null!;

    public override void Initialize()
    {
        base.Initialize();

        _domains.Register(this, Family);
    }

    public bool TryResolve<T>(
        GoapState state,
        EntityUid owner,
        StateKey<T>[] domains,
        [NotNullWhen(true)] out object? value) where T : notnull
    {
        value = null;

        if (GoapState.QueryDomain.TryGetParams(domains, out ProtoId<SearchQueryPrototype>? query))
        {
            if (!_proto.HasIndex(query)
                || !_searcher.TryGetBestResult(owner, state, query.Value, out var result))
                return false;

            value = result.Value;
            return true;
        }

        if (GoapState.QueryAllDomain.TryGetParams(domains, out query))
        {
            if (!_proto.HasIndex(query))
                return false;

            var results = _searcher.GetResults(owner, state, query.Value);

            if (results.Count == 0)
                return false;

            value = (T)results;
            return true;
        }

        return false;
    }
}
