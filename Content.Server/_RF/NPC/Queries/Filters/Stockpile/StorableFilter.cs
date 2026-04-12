using Content.Server._RF.Stockpile;
using Content.Server.NPC;
using Content.Shared._RF.NPC;

namespace Content.Server._RF.NPC.Queries.Filters.Stockpile;

/// <summary>
/// Filters entities for which there is space in the stockpile
/// </summary>
public sealed partial class StorableFilter : RfUtilityQueryFilter
{
    private StockpileSystem _stockpile = default!;
    private OwnershipSystem _ownership = default!;

    public override void Initialize(IEntityManager entManager)
    {
        base.Initialize(entManager);
        _stockpile = entManager.System<StockpileSystem>();
        _ownership = entManager.System<OwnershipSystem>();
    }

    public override bool Filter(EntityUid uid, NPCBlackboard blackboard)
    {
        foreach (var stock in _stockpile.AllStockpiles())
        {
            if (_ownership.HasOwner(uid, stock.Owner) && _stockpile.CanInsert(stock, uid))
                return true;
        }

        return false;
    }
}
