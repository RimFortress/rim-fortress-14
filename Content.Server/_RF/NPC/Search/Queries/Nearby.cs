using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Components;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared.Whitelist;
using Robust.Shared.Map;

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

    private readonly Dictionary<EntityUid, EntityCoordinates> _lastUpdates = new();
    private const float MovementUpdateThreshold = 2f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NpcSearcherComponent, MoveEvent>(OnSearcherMove);
    }

    private void OnSearcherMove(Entity<NpcSearcherComponent> ent, ref MoveEvent ev)
    {
        if (_lastUpdates.TryGetValue(ent, out var coords)
            && coords.EntityId == ev.NewPosition.EntityId
            && coords.TryDistance(EntityManager, ev.NewPosition, out var dist)
            && dist < MovementUpdateThreshold)
            return;

        _lastUpdates[ent] = ev.NewPosition;

        foreach (var (proto, live) in ent.Comp.Queries)
        {
            if (!TryGetQuery(proto, out var query))
                continue;

            Query.Clear();

            foreach (var uid in _lookup.GetEntitiesInRange(ent, query.Range, LookupFlags.Uncontained | LookupFlags.Dynamic | LookupFlags.Static))
            {
                if (Query.Count >= query.Limit)
                    return;

                if (!_whitelist.IsWhitelistPassOrNull(query.Whitelist, uid))
                    continue;

                Query.Add(uid);
            }

            var removed = new HashSet<EntityUid>();

            foreach (var uid in live.Results)
            {
                if (!Query.Contains(uid))
                    removed.Add(uid);
                else
                    Query.Remove(uid);
            }

            Searcher.ReportDirty(ent, proto, added: Query, removed: removed);
        }

        Query.Clear();
    }

    protected override void GetQuery(GoapState state, Nearby query)
    {
        foreach (var uid in _lookup.GetEntitiesInRange(SharedGoapSystem.Owner(state), query.Range))
        {
            if (Query.Count >= query.Limit)
                return;

            if (!_whitelist.IsWhitelistPassOrNull(query.Whitelist, uid))
                continue;

            Query.Add(uid);
        }
    }
}
