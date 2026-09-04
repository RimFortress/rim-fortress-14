using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared._RF.NPC.Search.Components;
using Content.Shared._RF.NPC.Search.Prototypes;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.NPC.Search.Systems;

/// <summary>
/// A system that handles search query filters.
/// </summary>
/// <typeparam name="T">Search filter type.</typeparam>
public abstract partial class NpcSearchFilterSystem<T> : EntitySystem where T : BaseSearchFilter<T>
{
    [Dependency] protected SharedGoapSystem Goap = default!;
    [Dependency] protected IPrototypeManager Proto = default!;
    [Dependency] protected SharedNpcSearcherSystem Searcher = default!;

    [Dependency] protected EntityQuery<GoapComponent> GoapQuery = default!;
    [Dependency] protected EntityQuery<SearchTrackedComponent> TrackedQuery = default!;

    private readonly Dictionary<ProtoId<SearchQueryPrototype>, HashSet<(int Index, T Obj)>> _filterPrototypes = new();
    protected IReadOnlyDictionary<ProtoId<SearchQueryPrototype>, HashSet<(int Index, T Obj)>> FilterPrototypes =>
        _filterPrototypes;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NpcSearcherComponent, GetSearchFilter<T>>(OnGetSearchFilter);

        Proto.PrototypesReloaded += args =>
        {
            if (args.WasModified<SearchQueryPrototype>())
                ReloadPrototypes();
        };

        ReloadPrototypes();
    }

    private void ReloadPrototypes()
    {
        _filterPrototypes.Clear();

        foreach (var prototype in Proto.EnumeratePrototypes<SearchQueryPrototype>())
        {
            var list = new HashSet<(int Index, T Obj)>();

            for (var i = 0; i < prototype.Filters.Count; i++)
            {
                IndexFilter(list, i, prototype.Filters[i]);
            }

            if (list.Count > 0)
                _filterPrototypes.Add(prototype, list);
        }
    }

    /// <summary>
    /// Registers a top-level Filter under its own type at its pipeline stage.
    /// If it's a composite (<see cref="ICompositeSearchFilter"/>), recursively
    /// registers every child under the SAME stage index — the composite is
    /// what actually occupies that slot in <see cref="SearchQueryPrototype.Filters"/>,
    /// so a change to any child must be reported against that stage, not one
    /// that doesn't exist for the child on its own.
    /// </summary>
    private static void IndexFilter(HashSet<(int, T)> list, int stage, SearchFilter filter)
    {
        if (filter is T tFilter)
            list.Add((stage, tFilter));

        if (filter is not ICompositeSearchFilter composite)
            return;

        foreach (var child in composite.Children)
        {
            IndexFilter(list, stage, child);
        }
    }

    private void OnGetSearchFilter(Entity<NpcSearcherComponent> ent, ref GetSearchFilter<T> ev)
    {
        ev.Result = Filter(ev.State, ev.Target, ev.Filter);
    }

    /// <summary>
    /// Checks whether the target entity should be filtered out from the search query.
    /// </summary>
    /// <param name="state">GoapState of the agent requesting the search.</param>
    /// <param name="target">Target entity.</param>
    /// <param name="filter">Search filter.</param>
    protected abstract bool Filter(GoapState state, EntityUid target, T filter);

    /// <summary>
    /// Recalculates the result of the target entity filter and updates the data in the search.
    /// </summary>
    /// <param name="ent">Target entity.</param>
    [PublicAPI]
    protected void DirtyFilter(Entity<SearchTrackedComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        foreach (var ((agent, protoId), stage) in ent.Comp.Tracking)
        {
            if (!FilterPrototypes.TryGetValue(protoId, out var indexes))
                continue;

            foreach (var (ind, filter) in indexes)
            {
                DirtyFilter(agent, protoId, ent, filter, stage, ind);
            }
        }
    }

    /// <summary>
    /// Recalculates the filter result for all queries processed by this agent.
    /// </summary>
    /// <param name="ent">Target agent.</param>
    [PublicAPI]
    protected void DirtyAgentFilter(Entity<NpcSearcherComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false)
            || !GoapQuery.TryComp(ent, out var goap))
            return;

        var agent = new Entity<GoapComponent?>(ent, goap);

        foreach (var (protoId, indexes) in FilterPrototypes)
        {
            if (!SharedNpcSearcherSystem.TryGetLiveResult(ent.Comp, protoId, out var live))
                continue;

            foreach (var uid in live.Tracked)
            {
                if (!TrackedQuery.TryComp(uid, out var comp)
                    || !SharedNpcSearcherSystem.TryGetTracker(comp, (agent, protoId), out var tacker))
                    continue;

                foreach (var (ind, filter) in indexes)
                {
                    DirtyFilter(agent, protoId, uid, filter, tacker, ind);
                }
            }
        }
    }

    /// <inheritdoc cref="DirtyFilter(Entity{SearchTrackedComponent?})"/>
    [PublicAPI]
    protected void DirtyFilter(
        Entity<GoapComponent?> agent,
        ProtoId<SearchQueryPrototype> protoId,
        EntityUid target,
        T filter,
        SearchTrackEntry track,
        int index)
    {
        if (!Resolve(agent, ref agent.Comp) || !Proto.Resolve(protoId, out var proto))
            return;

        switch (proto.Filters[index].Filter(agent.Comp.State, target, Searcher))
        {
            case true when track.FilterStage == index - 1:
                Searcher.ReportDirty(agent, protoId, index, added: new() { target });
                break;
            case false when track.FilterStage >= index:
                Searcher.ReportRejected(agent, protoId, index, new() { target });
                break;
        }
    }
}

/// <summary>
/// A system that provides built-in logic for search filters that use GOAP state keys for searching.
/// </summary>
/// <typeparam name="TFilter">Search filter type.</typeparam>
/// <typeparam name="TKey">The key type on which the filter depends.</typeparam>
public abstract class NpcSearchGoapKeyFilterSystem<TFilter, TKey> : NpcSearchFilterSystem<TFilter>
    where TFilter : BaseSearchFilter<TFilter>
    where TKey : notnull
{
    private readonly
        Dictionary<StateKey<TKey>, HashSet<ProtoId<SearchQueryPrototype>>> _types = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GoapStateValueSet<TKey>>(OnGoapSetValue);
        SubscribeLocalEvent<GoapStateValueRemove<TKey>>(OnGoapRemoveValue);
        Subs.ProtoReload<SearchQueryPrototype>(Proto, ReloadPrototypes);

        ReloadPrototypes();
    }

    private void ReloadPrototypes()
    {
        _types.Clear();

        foreach (var proto in Proto.EnumeratePrototypes<SearchQueryPrototype>())
        {
            foreach (var f in proto.Filters)
            {
                if (f is not TFilter filter)
                    continue;

                foreach (var key in GetSubscribeKeys(filter))
                {
                    if (!_types.TryAdd(key, new() { proto }))
                        _types[key].Add(proto);
                }
            }
        }
    }

    private void OnGoapSetValue(ref GoapStateValueSet<TKey> ev)
    {
        OnValueChanged(ev.Agent, ev.Key);
    }

    private void OnGoapRemoveValue(ref GoapStateValueRemove<TKey> ev)
    {
        OnValueChanged(ev.Agent, ev.Key);
    }

    private void OnValueChanged(EntityUid agentUid, StateKey<TKey> key)
    {
        if (!HasComp<NpcSearcherComponent>(agentUid)
            || !_types.TryGetValue(key, out var prototypes)
            || !GoapQuery.TryComp(agentUid, out var goap))
            return;

        var agent = new Entity<GoapComponent?>(agentUid, goap);

        var enumerator = EntityQueryEnumerator<SearchTrackedComponent>();
        while (enumerator.MoveNext(out var uid, out var comp))
        {
            foreach (var protoId in prototypes)
            {
                if (!SharedNpcSearcherSystem.TryGetTracker(comp, (agentUid, protoId), out var tracker)
                    || !FilterPrototypes.TryGetValue(protoId, out var filters))
                    continue;

                foreach (var (index, filter) in filters)
                {
                    DirtyFilter(agent, protoId, uid, filter, tracker, index);
                }
            }
        }
    }

    /// <summary>
    /// Returns a list of keys on which this filter depends.
    /// </summary>
    protected abstract HashSet<StateKey<TKey>> GetSubscribeKeys(TFilter filter);
}
