using System.Linq;
using Content.Server._RF.NPC.Components;
using Content.Server._RF.Stockpile;
using Content.Server.NPC;

namespace Content.Server._RF.NPC.Queries.Queries;

/// <summary>
/// Returns all entities from NPC owners' stockpiles
/// </summary>
public sealed partial class StoredQuery : RfUtilityQuery
{
    private StockpileSystem _stockpile = default!;

    public override void Initialize(IEntityManager entManager)
    {
        base.Initialize(entManager);
        _stockpile = entManager.System<StockpileSystem>();
    }

    public override HashSet<EntityUid> Query(NPCBlackboard blackboard)
    {
        var query = new HashSet<EntityUid>();
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!EntityManager.TryGetComponent(owner, out ControllableNpcComponent? control)
            || control.CanControl.Count == 0)
            return query;

        var canControl = control.CanControl.ToList();

        foreach (var stock in _stockpile.AllStockpiles())
        {
            if (!canControl.Contains(stock.Owner))
                continue;

            query.UnionWith(stock.Entities);
        }

        return query;
    }
}
