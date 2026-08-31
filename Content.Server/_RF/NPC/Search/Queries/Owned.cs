using Content.Shared._RF.NPC.Components;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Components;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared._RF.NPC.Systems;

namespace Content.Server._RF.NPC.Search.Queries;

/// <summary>
/// Returns all entities that share the same owner with the agent.
/// </summary>
public sealed partial class Owned : BaseSearchQuery<Owned>;

public sealed class OwnedQuerySystem : NpcSearchQuerySystem<Owned>
{
    [Dependency] private readonly OwnershipSystem _ownership = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OwnershipComponent, OwnershipAddedEvent>(OnOwnershipAdded);
        SubscribeLocalEvent<SearchTrackedComponent, OwnershipRemovedEvent>(OnOwnershipRemoved);
    }

    private void OnOwnershipAdded(Entity<OwnershipComponent> ent, ref OwnershipAddedEvent ev)
    {
        if (ev.Owner != ent.Owner)
            return; // The event is triggered twice—once for the owner and once for the owned entity

        foreach (var uid in ent.Comp.Owned)
        {
            if (!SearcherQuery.TryComp(uid, out var comp))
                continue;

            foreach (var (proto, _) in comp.Queries)
            {
                if (QueryTypeIs(proto))
                    Searcher.ReportDirty(uid, proto, added: new() { ev.Owned });
            }
        }
    }

    private void OnOwnershipRemoved(Entity<SearchTrackedComponent> ent, ref OwnershipRemovedEvent ev)
    {
        if (ev.Owner != ent.Owner)
            return;

        foreach (var ((agent, proto), _) in ent.Comp.Tracking)
        {
            if (QueryTypeIs(proto))
                Searcher.ReportDirty(agent, proto, removed: new() { ev.Owned });
        }
    }

    protected override void GetQuery(GoapState state, Owned query)
    {
        var enumerator = _ownership.GetEntitiesEnumerator(SharedGoapSystem.Owner(state));

        while (Query.Count < query.Limit && enumerator.MoveNext(out var uid))
        {
            if (!Deleted(uid))
                Query.Add(uid);
        }
    }
}
