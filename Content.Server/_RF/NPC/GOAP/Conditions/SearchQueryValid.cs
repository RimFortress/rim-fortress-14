using Content.Server._RF.NPC.GOAP.Actions;
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
/// Always use this condition in the <see cref="SearchQuery"/> action to avoid planning bugs.
/// </remarks>
public sealed partial class SearchQueryValid : BaseGoapCondition<SearchQueryValid>
{
    /// <summary>
    /// Search query prototype;
    /// </summary>
    [DataField(required: true)]
    public ProtoId<SearchQueryPrototype> Query;
}

public sealed class SearchQueryValidSystem : GoapConditionSystem<SearchQueryValid>
{
    [Dependency] private readonly SharedNpcSearcherSystem _npcSearcher = default!;

    protected override bool ConditionCheck(EntityUid uid, GoapState state, SearchQueryValid condition)
    {
        var result = _npcSearcher.GetResults(uid, state, condition.Query).Count;
        CreateDump(state, condition, $"query count was {result}");
        return result > 0;
    }
}
