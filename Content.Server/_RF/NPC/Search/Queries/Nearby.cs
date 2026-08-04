using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Systems;

namespace Content.Server._RF.NPC.Search.Queries;

/// <summary>
/// Returns all entities within a specified radius of the agent.
/// </summary>
public sealed partial class Nearby : BaseSearchQuery<Nearby>
{
    /// <summary>
    /// Radius.
    /// </summary>
    [DataField]
    public float Range = 10f;
}

public sealed class NearbyQuerySystem : NpcSearchQuerySystem<Nearby>
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;

    protected override void GetQuery(GoapState state, Nearby query)
    {
        _lookup.GetEntitiesInRange(state.GetValue(GoapState.Owner), query.Range, Query);

        if (Query.Count <= query.Limit)
            return;

        var index = 0;
        Query.RemoveWhere(_ => index++ >= query.Limit);
    }
}
