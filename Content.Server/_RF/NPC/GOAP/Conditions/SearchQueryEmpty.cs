using Content.Server._RF.NPC.GOAP.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search.Prototypes;
using Content.Shared._RF.NPC.Search.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.GOAP.Conditions;

/// <summary>
/// Checks whether there is at least one entity that matches the search query.
/// </summary>
/// <remarks>
/// Always use this condition in the SearchQuery action to avoid planning bugs.
/// </remarks>
public sealed partial class SearchQueryEmpty : BaseGoapCondition<SearchQueryEmpty>
{
    /// <summary>
    /// Search query prototype;
    /// </summary>
    [DataField(required: true)]
    public ProtoId<SearchQueryPrototype> Query;
}

public sealed class SearchQueryEmptySystem : GoapConditionSystem<SearchQueryEmpty>
{
    [Dependency] private readonly NpcSearcherSystem _npcSearcher = default!;

    protected override bool ConditionCheck(EntityUid uid, GoapState state, SearchQueryEmpty condition)
        => _npcSearcher.GetResults(uid, state, condition.Query).Count > 0;
}
