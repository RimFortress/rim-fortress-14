using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared._RF.Stockpile.Systems;

namespace Content.Server._RF.NPC.Search.Filters.Stockpile;

/// <summary>
/// Filters entities located in a container in the stockpile.
/// </summary>
public sealed partial class StoredInStockContainer : BaseSearchFilter<StoredInStockContainer>;

public sealed class StoredInStockContainerSearchFilterSystem : NpcSearchFilterSystem<StoredInStockContainer>
{
    [Dependency] private readonly StockpileSystem _stockpile = default!;

    protected override bool Filter(GoapState state, EntityUid target, StoredInStockContainer filter)
        => _stockpile.StoredInContainer(target);
}
