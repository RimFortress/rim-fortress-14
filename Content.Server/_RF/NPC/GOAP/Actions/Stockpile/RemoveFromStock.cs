using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared._RF.Stockpile.Components;
using Content.Shared._RF.Stockpile.Systems;

namespace Content.Server._RF.NPC.GOAP.Actions.Stockpile;

/// <summary>
/// Removes the entity from those assigned to the stock.
/// </summary>
public sealed partial class RemoveFromStock : BaseGoapAction<RemoveFromStock>
{
    /// <summary>
    /// Target entity.
    /// </summary>
    [DataField(required: true)]
    public StateKey<EntityUid> TargetKey;
}

public sealed partial class RemoveFromStockGoapAction : GoapActionSystem<RemoveFromStock>
{
    [Dependency] private StockpileSystem _stockpile = default!;
    [Dependency] private EntityQuery<StockpileContentComponent> _query;

    protected override bool ActionStartup(Entity<GoapComponent> ent, RemoveFromStock action)
    {
        if (!TryGet(ent, action.TargetKey, out var target))
            return false;

        if (!_query.TryComp(target, out var comp))
        {
            ComponentNotFound<StockpileContentComponent>(target);
            return false;
        }

        if (!_stockpile.TryGetStock(comp.Stock, out var stock))
        {
            CreateDump("stockpile not found");
            return false;
        }

        return _stockpile.RemoveEntity(stock.Value, target);
    }
}
