using System.Linq;
using Content.Shared._RF.NPC.Engagement;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Components;
using Content.Shared._RF.NPC.Search.Systems;

namespace Content.Server._RF.NPC.Search.Queries;

/// <summary>
/// Returns the entities that are in a key of the state of the agent. They are whatever the one who has put them there made them,
/// so it is a fixed set of entities, such as those that an event is about, whose classes (alive, down, dead) are told apart by
/// the filters of the query. The result follows the key: when its value is set or removed (with <c>SharedGoapSystem.SetValue</c>,
/// which says so), the entities that are no longer in it leave the result, and those which are new get into it.
/// The key can be one of a domain, such as that of the owner of the situation (<c>Engagement/Raid/Owner/Targets</c>): then it
/// follows the key of the owner, and the agent gets the entities when it joins the situation.
/// </summary>
public sealed partial class KeyEntities : BaseSearchQuery<KeyEntities>
{
    /// <summary>
    /// The key with the entities.
    /// </summary>
    [DataField(required: true)]
    public StateKey<HashSet<EntityUid>> Key;
}

public sealed partial class KeyEntitiesQuerySystem : NpcSearchQuerySystem<KeyEntities>
{
    [Dependency] private SharedGoapSystem _goap = null!;

    [SubscribeLocalEvent]
    private void OnSet(ref GoapStateValueSet<HashSet<EntityUid>> ev) => Update(ev.Agent, ev.Key, ev.Value);

    [SubscribeLocalEvent]
    private void OnRemove(ref GoapStateValueRemove<HashSet<EntityUid>> ev) => Update(ev.Agent, ev.Key, null);

    [SubscribeLocalEvent]
    private void OnJoined(Entity<NpcSearcherComponent> ent, ref EngagementRoleJoined ev)
    {
        if (ev.Actor != ent.Owner || !TryComp(ent, out GoapComponent? goap))
            return;

        // What the situation has, the agent has now.
        foreach (var key in DomainKeys(ent.Comp))
        {
            _goap.TryGetValue(goap.State, key, out var value);
            Update(ent, key, value);
        }
    }

    protected override void GetQuery(GoapState state, KeyEntities query)
    {
        if (!_goap.TryGetValue(state, query.Key, out var entities))
            return;

        foreach (var uid in entities)
        {
            if (Query.Count >= query.Limit)
                break;

            if (!TerminatingOrDeleted(uid))
                Query.Add(uid);
        }
    }

    /// <summary>
    /// The keys of the queries of the searcher that are not its own, but of a domain.
    /// </summary>
    private IEnumerable<StateKey<HashSet<EntityUid>>> DomainKeys(NpcSearcherComponent searcher)
    {
        foreach (var proto in searcher.Queries.Keys)
        {
            if (TryGetQuery(proto, out var query) && query.Key.Id.Contains(GoapState.KeyDomainSeparator))
                yield return query.Key;
        }
    }

    /// <summary>
    /// Makes the results of the queries of the agent that are about the key what the value of the key is now.
    /// </summary>
    private void Update(EntityUid agent, StateKey<HashSet<EntityUid>> key, HashSet<EntityUid>? value)
    {
        if (!SearcherQuery.TryComp(agent, out var searcher))
            return;

        foreach (var (proto, live) in searcher.Queries.ToArray())
        {
            if (!TryGetQuery(proto, out var query) || query.Key != key)
                continue;

            var removed = new HashSet<EntityUid>();
            var added = new HashSet<EntityUid>();

            foreach (var uid in live.Tracked)
            {
                if (value == null || !value.Contains(uid))
                    removed.Add(uid);
            }

            if (value != null)
            {
                foreach (var uid in value)
                {
                    if (live.Tracked.Count - removed.Count + added.Count >= query.Limit)
                        break;

                    if (!live.Tracked.Contains(uid) && !TerminatingOrDeleted(uid))
                        added.Add(uid);
                }
            }

            Searcher.ReportDirty(agent, proto, added: added, removed: removed);
        }
    }
}
