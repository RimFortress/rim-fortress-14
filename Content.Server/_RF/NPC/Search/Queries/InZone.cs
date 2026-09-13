using Content.Shared._RF.NPC.Components;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Components;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared._RF.NPC.Systems;
using Content.Shared._RF.Zoning;
using Content.Shared._RF.Zoning.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.Search.Queries;

/// <summary>
/// Returns a list of all entities located in zones of a specific type that were created by the agent's owners.
/// </summary>
public sealed partial class InZone : BaseSearchQuery<InZone>
{
    /// <summary>
    /// Types of zones to include in the search;
    /// if the list is empty, the search will cover all zones.
    /// </summary>
    [DataField]
    public HashSet<EntProtoId<ZoneComponent>> Zones = new();
}

public sealed partial class InZoneSearchQuerySystem : NpcSearchQuerySystem<InZone>
{
    [Dependency] private OwnershipSystem _ownership = default!;

    [SubscribeLocalEvent]
    private void OnZoneInsert(Entity<OwnershipComponent> ent, ref EntityEnteredZone args)
    {
        if (ent.Owner != args.Uid || Prototype(args.ZoneUid) is not { } zone)
            return;

        foreach (var uid in _ownership.GetOwners(ent))
        {
            if (!SearcherQuery.TryComp(uid, out var comp))
                continue;

            foreach (var (proto, _) in comp.Queries)
            {
                if (!TryGetQuery(proto, out var query) || !query.Zones.Contains(zone.ID))
                    continue;

                Searcher.ReportDirty(uid, proto, added: new() { ent });
            }
        }
    }

    [SubscribeLocalEvent]
    private void OnZoneLeave(Entity<SearchTrackedComponent> ent, ref EntityLeavedZone args)
    {
        if (ent.Owner != args.Uid || Prototype(args.ZoneUid) is not { } zone)
            return;

        foreach (var ((agent, proto), _) in ent.Comp.Tracking)
        {
            if (!TryGetQuery(proto, out var query) || !query.Zones.Contains(zone.ID))
                continue;

            Searcher.ReportDirty(agent, proto, removed: new() { ent });
        }
    }

    protected override void GetQuery(GoapState state, InZone query)
    {
        var owner = SharedGoapSystem.Owner(state);
        var enumerator = _ownership.GetEntitiesEnumerator<ZoneComponent>(owner);

        while (enumerator.MoveNext(out var zone, out var comp))
        {
            if (query.Zones.Count != 0 && (Prototype(zone) is not { } proto || !query.Zones.Contains(proto.ID)))
                continue;

            foreach (var uid in comp.Entities)
            {
                if (Query.Count >= query.Limit)
                    return;

                Query.Add(uid);
            }
        }
    }
}

