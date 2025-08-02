using Content.Server._RF.Stockpile;
using Content.Server.NPC;

namespace Content.Server._RF.NPC.Queries.Filters.Stockpile;

/// <summary>
/// Filters entities that can be stockpiled to at least one stockpile,
/// supplied from the stockpile where they are currently located
/// </summary>
public sealed partial class CanInsertInSuppliedFilter : RfUtilityQueryFilter
{
    private StockpileSystem _stockpile = default!;

    public override void Initialize(IEntityManager entManager)
    {
        base.Initialize(entManager);
        _stockpile = entManager.System<StockpileSystem>();
    }

    public override bool Filter(EntityUid uid, NPCBlackboard blackboard)
    {
        if (!_stockpile.TryGetContainingStock(uid, out var stock))
            return false;

        foreach (var id in stock.SuppliedStockpiles)
        {
            if (_stockpile.TryGetStock(id, out var supplied)
                && _stockpile.CanInsert(supplied, uid))
                return true;
        }

        return false;
    }
}
