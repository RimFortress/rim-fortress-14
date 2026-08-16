using Content.Server._RF.NPC.GOAP.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Components;
using Content.Shared._RF.NPC.Search.Prototypes;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared._RF.Stockpile.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.Search.Filters.Stockpile;

/// <summary>
/// Filters stockpiles where the target entity can be stored.
/// </summary>
public sealed partial class StockCanInsert : BaseSearchFilter<StockCanInsert>
{
    /// <summary>
    /// Target entity to insert.
    /// </summary>
    [DataField(required: true)]
    public StateKey<EntityUid> TargetKey;
}

public sealed class StockCanInsertSearchFilterSystem : NpcSearchFilterSystem<StockCanInsert>
{
    [Dependency] private readonly GoapSystem _goap = default!;
    [Dependency] private readonly StockpileSystem _stockpile = default!;

    private readonly
        Dictionary<StateKey<EntityUid>, HashSet<ProtoId<SearchQueryPrototype>>> _types = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NpcSearcherComponent, GoapStateValueSet<EntityUid>>(OnGoapSetValue);

        Proto.PrototypesReloaded += args =>
        {
            if (args.WasModified<SearchQueryPrototype>())
                ReloadPrototypes();
        };

        ReloadPrototypes();
    }

    private void ReloadPrototypes()
    {
        _types.Clear();

        foreach (var proto in Proto.EnumeratePrototypes<SearchQueryPrototype>())
        {
            foreach (var f in proto.Filters)
            {
                if (f is not StockCanInsert filter)
                    continue;

                if (!_types.TryAdd(filter.TargetKey, new() { proto }))
                    _types[filter.TargetKey].Add(proto);
            }
        }
    }

    private void OnGoapSetValue(Entity<NpcSearcherComponent> ent, ref GoapStateValueSet<EntityUid> ev)
    {
        if (!_types.TryGetValue(ev.Key, out var prototypes)
            || !GoapQuery.TryComp(ent, out var goap))
            return;

        var agent = new Entity<GoapComponent?>(ent, goap);

        var enumerator = EntityQueryEnumerator<SearchTrackedComponent>();
        while (enumerator.MoveNext(out var uid, out var comp))
        {
            foreach (var protoId in prototypes)
            {
                if (!SharedNpcSearcherSystem.TryGetTracker(comp, (ent, protoId), out var tracker)
                    || !FilterPrototypes.TryGetValue(protoId, out var filters))
                    continue;

                foreach (var (index, filter) in filters)
                {
                    DirtyFilter(agent, protoId, uid, filter, tracker, index);
                }
            }
        }
    }

    protected override bool Filter(GoapState state, EntityUid target, StockCanInsert filter)
        => _goap.TryGetValue(state, filter.TargetKey, out var uid)
           && _stockpile.TryGetStock(target, out var stock)
           && _stockpile.CanInsert(stock.Value, uid);
}
