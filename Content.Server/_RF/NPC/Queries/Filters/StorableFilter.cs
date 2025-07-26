using Content.Server._RF.Stockpile;
using Content.Server.NPC;
using Content.Shared._RF.NPC;

namespace Content.Server._RF.NPC.Queries.Filters;

/// <summary>
/// Filters entities for which there is space in the stockpile
/// </summary>
public sealed partial class StorableFilter : RfUtilityQueryFilter
{
    private StockpileSystem _stockpile = default!;

    private EntityQuery<OwnedComponent> _ownerQuery;

    public override void Initialize(IEntityManager entManager)
    {
        base.Initialize(entManager);
        _stockpile = entManager.System<StockpileSystem>();
        _ownerQuery = entManager.GetEntityQuery<OwnedComponent>();
    }

    public override bool Filter(EntityUid uid, NPCBlackboard blackboard)
    {
        if (!_ownerQuery.TryComp(uid, out var comp))
            return false;

        foreach (var stock in _stockpile.AllStockpiles())
        {
            if (comp.Owners.Contains(stock.Owner) && _stockpile.CanInsert(stock, uid))
                return true;
        }

        return false;
    }
}
