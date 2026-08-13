using Content.Server.Storage.EntitySystems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared._RF.Stockpile.Systems;
using Content.Shared.Storage.Components;

namespace Content.Server._RF.NPC.Search.Filters.Stockpile;

/// <summary>
/// Filters entities in the warehouse that can be moved to a container.
/// </summary>
public sealed partial class CantInsertInStockContainer : BaseSearchFilter<CantInsertInStockContainer>;

public sealed class CantInsertInStockContainerSearchSystem : NpcSearchFilterSystem<CantInsertInStockContainer>
{
    [Dependency] private readonly StockpileSystem _stockpile = default!;
    [Dependency] private readonly EntityStorageSystem _storage = default!;
    [Dependency] private readonly EntityQuery<EntityStorageComponent> _storageQuery = default!;

    protected override bool Filter(GoapState state, EntityUid target, CantInsertInStockContainer filter)
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
}
