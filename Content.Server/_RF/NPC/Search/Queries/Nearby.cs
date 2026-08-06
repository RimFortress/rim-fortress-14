using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared.Whitelist;

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

    /// <summary>
    /// Whitelist for including in the query.
    /// </summary>
    [DataField]
    public EntityWhitelist? Whitelist;
}

public sealed class NearbyQuerySystem : NpcSearchQuerySystem<Nearby>
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;

    protected override void GetQuery(GoapState state, Nearby query)
    {
        foreach (var uid in _lookup.GetEntitiesInRange(state.GetValue(GoapState.Owner), query.Range))
        {
            if (Query.Count >= query.Limit)
                return;

            if (!_whitelist.IsWhitelistPassOrNull(query.Whitelist, uid))
                continue;

            Query.Add(uid);
        }
    }
}
