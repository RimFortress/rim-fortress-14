using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared._RF.Stockpile;
using Content.Shared._RF.Stockpile.Systems;

namespace Content.Server._RF.NPC.Search.Considerations;

/// <summary>
/// Evaluates stockpiles based on the total number of stockpiles they supply.
/// </summary>
public sealed partial class StockTotalSupplied : BaseSearchConsideration<StockTotalSupplied>;

public sealed partial class StockTotalSuppliedSearchConsiderationSystem : NpcSearchConsiderationSystem<StockTotalSupplied>
{
    [Dependency] private readonly StockpileSystem _stockpile = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeRescoreEvent<StockpileSupplyingAdded>();
        SubscribeRescoreEvent<StockpileSupplyingRemoved>();
    }

    protected override float GetScore(GoapState state, EntityUid target, StockTotalSupplied con)
        => _stockpile.TryGetStock(target, out var stock) ? _stockpile.GetTotalSupplied(stock.Value) : 0f;
}
