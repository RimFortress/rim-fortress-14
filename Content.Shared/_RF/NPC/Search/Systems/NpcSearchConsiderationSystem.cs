using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared._RF.NPC.Search.Components;
using Content.Shared._RF.NPC.Search.Prototypes;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.NPC.Search.Systems;

/// <summary>
/// A system that handles search query considerations.
/// </summary>
/// <typeparam name="T">Search considerations type.</typeparam>
public abstract partial class NpcSearchConsiderationSystem<T> : EntitySystem where T : BaseSearchConsideration<T>
{
    [Dependency] protected SharedGoapSystem Goap = default!;
    [Dependency] protected IPrototypeManager Proto = default!;
    [Dependency] protected SharedNpcSearcherSystem Searcher = default!;

    private readonly Dictionary<ProtoId<SearchQueryPrototype>, HashSet<(int Index, T Obj)>>
        _considerationPrototypes = new();

    protected IReadOnlyDictionary<ProtoId<SearchQueryPrototype>, HashSet<(int Index, T Obj)>>
        ConsiderationPrototypes => _considerationPrototypes;

    public override void Initialize()
    {
        base.Initialize();

        Proto.PrototypesReloaded += args =>
        {
            if (args.WasModified<SearchQueryPrototype>())
                ReloadPrototypes();
        };

        ReloadPrototypes();
    }

    private void ReloadPrototypes()
    {
        _considerationPrototypes.Clear();

        foreach (var prototype in Proto.EnumeratePrototypes<SearchQueryPrototype>())
        {
            var list = new HashSet<(int Index, T Obj)>();

            for (var i = 0; i < prototype.Considerations.Count; i++)
            {
                if (prototype.Considerations[i] is T con)
                    list.Add((i, con));
            }

            if (list.Count > 0)
                _considerationPrototypes.Add(prototype, list);
        }
    }

    [SubscribeLocalEvent]
    private void OnGetSearchScore(Entity<NpcSearcherComponent> ent, ref GetSearchScore<T> ev)
    {
        ev.Result = GetScore(ev.State, ev.Target, ev.Con);
    }

    /// <summary>
    /// Returns result of the consideration of the target entity in the search query.
    /// </summary>
    /// <param name="state">GoapState of the agent requesting the search.</param>
    /// <param name="target">Target entity.</param>
    /// <param name="con">Search consideration.</param>
    protected abstract float GetScore(GoapState state, EntityUid target, T con);

    [PublicAPI]
    protected void SubscribeRescoreEvent<TComp, TEvent>()
        where TComp : Component
        where TEvent : notnull
    {
        Subs.SubscribeLocalEvent((Entity<TComp> ent, ref TEvent _) => Rescore(ent.Owner));
    }

    [PublicAPI]
    protected void SubscribeRescoreEvent<TEvent>() where TEvent : notnull
    {
        Subs.SubscribeLocalEvent((Entity<SearchTrackedComponent> ent, ref TEvent _) => Rescore(ent.AsNullable()));
    }

    /// <summary>
    /// Notifies the searcher that it needs to recalculate all previous considerations regarding this agent.
    /// </summary>
    /// <param name="agent">Agent entity</param>
    [PublicAPI]
    protected void RescoreAll(Entity<NpcSearcherComponent?> agent)
    {
        if (!Resolve(agent, ref agent.Comp, false))
            return;

        foreach (var (protoId, live) in agent.Comp.Queries)
        {
            if (!ConsiderationPrototypes.TryGetValue(protoId, out var indexes))
                continue;

            foreach (var (ind, _) in indexes)
            {
                Searcher.ReportRescore(agent, protoId, ind, live.Tracked);
            }
        }
    }

    /// <summary>
    /// Notifies the search engine that the considerations regarding this entity need to be recalculated.
    /// </summary>
    /// <param name="ent">Target entity.</param>
    [PublicAPI]
    protected void Rescore(Entity<SearchTrackedComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        foreach (var ((agent, protoId), _) in ent.Comp.Tracking)
        {
            if (!ConsiderationPrototypes.TryGetValue(protoId, out var indexes))
                continue;

            foreach (var (ind, _) in indexes)
            {
                Searcher.ReportRescore(agent, protoId, ind, new() { ent });
            }
        }
    }
}
