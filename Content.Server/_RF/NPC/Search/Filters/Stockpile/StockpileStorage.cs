using Content.Server.Storage.EntitySystems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Components;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared._RF.NPC.Systems;
using Content.Shared._RF.Stockpile;
using Content.Shared._RF.Stockpile.Components;
using Content.Shared._RF.Stockpile.Systems;
using Content.Shared.Storage.Components;

namespace Content.Server._RF.NPC.Search.Filters.Stockpile;

/// <summary>
/// Filters entities based on whether they can be stored in any of the entity owner's stockpiles.
/// </summary>
public sealed partial class StockpileStorage : BaseSearchFilter<StockpileStorage>
{
    /// <summary>
    /// If true, only entities that are already in stock and can be transferred
    /// to another stockpile supplied with this stock will be filtered.
    /// </summary>
    [DataField]
    public bool SupplyingOnly;

    /// <summary>
    /// If true, only entities that are already in the stockpile and
    /// can be moved to a container in that stockpile will be filtered.
    /// </summary>
    [DataField]
    public bool MoveToContainer;

    /// <summary>
    /// If true, only entities already in the container in the stock will be filtered.
    /// </summary>
    [DataField]
    public bool StoredInContainer;
}

public sealed partial class CanInsertInStockSearchFilterSystem : NpcSearchFilterSystem<StockpileStorage>
{
    [Dependency] private StockpileSystem _stockpile = default!;
    [Dependency] private OwnershipSystem _ownership = default!;
    [Dependency] private EntityStorageSystem _storage = default!;
    [Dependency] private readonly EntityQuery<EntityStorageComponent> _storageQuery = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SearchTrackedComponent, StockEntityInserted>((ent, ref _) => DirtyFilter(ent.Owner));
        SubscribeLocalEvent<SearchTrackedComponent, StockEntityRemoved>((ent, ref _) => DirtyFilter(ent.Owner));
    }

    [SubscribeLocalEvent]
    private void OnStockSettingsChanged(StockSettingsChanged ev)
    {
        var enumerator = _ownership.GetEntitiesEnumerator<SearchTrackedComponent>(ev.StockUid);
        while (enumerator.MoveNext(out var uid, out var comp))
        {
            DirtyFilter(new(uid, comp));
        }
    }

    protected override bool Filter(GoapState state, EntityUid target, StockpileStorage filter)
    {
        if (filter.SupplyingOnly)
        {
            if (!_stockpile.TryGetContainingStock(target, out var stock))
                return false;

            foreach (var uid in stock.Value.Comp.Supplied)
            {
                if (_stockpile.TryGetStock(uid, out var supplied)
                    && _stockpile.CanInsert(supplied.Value, target))
                    return true;
            }

            return false;
        }

        if (filter.MoveToContainer)
        {
            if (!_stockpile.TryGetContainingStock(target, out var stock))
                return false;

            foreach (var uid in stock.Value.Comp.Stored)
            {
                if (_storageQuery.TryComp(uid, out var storageComp)
                    && _storage.CanInsert(target, uid, storageComp))
                    return true;
            }

            return false;
        }

        if (filter.StoredInContainer)
            return _stockpile.StoredInContainer(target);

        var enumerator = _ownership.GetEntitiesEnumerator<StockpileComponent>(target);
        while (enumerator.MoveNext(out var uid, out var comp))
        {
            if (_stockpile.CanInsert(new(uid, comp), target))
                return true;
        }

        return false;
    }
}
