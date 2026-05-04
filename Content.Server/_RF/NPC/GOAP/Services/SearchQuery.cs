using System.Threading;
using System.Threading.Tasks;
using Content.Server._RF.NPC.GOAP.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search.Prototypes;
using Content.Shared._RF.NPC.Search.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.GOAP.Services;

/// <summary>
/// Searches for an entity based on the search query and saves it to GoapState.
/// </summary>
public sealed partial class SearchQuery : BaseGoapService<SearchQuery>
{
    /// <summary>
    /// Search query prototype;
    /// </summary>
    [DataField(required: true)]
    public ProtoId<SearchQueryPrototype> Query;

    /// <summary>
    /// The key in which the result will be stored.
    /// </summary>
    [DataField]
    public StateKey<EntityUid> TargetKey = "Target";
}

public sealed class SearchQuerySystem : GoapServiceSystem<SearchQuery>
{
    [Dependency] private readonly NpcSearcherSystem _npcSearcher = default!;

    protected override async Task<GoapState?> Check(GoapState state, SearchQuery service, CancellationToken cancellation)
    {
        if (_npcSearcher.TryGetBestResult(state, service.Query, out var result))
            return new((service.TargetKey, result.Value));

        CreateDump(state, service, $"search query '{service.Query}' was empty");
        return null;
    }
}
