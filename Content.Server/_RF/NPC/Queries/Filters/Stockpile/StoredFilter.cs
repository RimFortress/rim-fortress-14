using Content.Server._RF.Stockpile;
using Content.Server.NPC;

namespace Content.Server._RF.NPC.Queries.Filters.Stockpile;

/// <summary>
/// Filters the entities stored in the stockpile
/// </summary>
public sealed partial class StoredFilter : RfUtilityQueryFilter
{
    private StockpileSystem _stockpile = default!;

    public override void Initialize(IEntityManager entManager)
    {
        base.Initialize(entManager);
        _stockpile = entManager.System<StockpileSystem>();
    }

    public override bool Filter(EntityUid uid, NPCBlackboard blackboard)
    {
        foreach (var stock in _stockpile.AllStockpiles())
        {
            if (stock.ContainsEntity(uid))
                return true;
        }

        return false;
    }
}
