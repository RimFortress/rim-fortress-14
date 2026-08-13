using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared._RF.Stockpile.Systems;

namespace Content.Server._RF.NPC.Search.Filters.Stockpile;

/// <summary>
/// Filters entities that can be transferred to any supplied stockpile.
/// </summary>
public sealed partial class CanSupplyStock : BaseSearchFilter<CanSupplyStock>;

public sealed class CanSupplyStockSearchFilterSystem : NpcSearchFilterSystem<CanSupplyStock>
{
    [Dependency] private readonly StockpileSystem _stockpile = default!;

    protected override bool Filter(GoapState state, EntityUid target, CanSupplyStock filter)
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
}
