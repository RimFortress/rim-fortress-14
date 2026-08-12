using Content.Server._RF.NPC.GOAP.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared._RF.Stockpile.Systems;

namespace Content.Server._RF.NPC.Search.Filters.Stockpile;

/// <summary>
/// Filters stockpiles where the target entity can be stored.
/// </summary>
public sealed partial class StockCanInsert : BaseSearchFilter<StockCanInsert>
{
    /// <summary>
    /// Target entity to insert.
    /// </summary>
    [DataField(required: true)]
    public StateKey<EntityUid> TargetKey;
}

public sealed class StockCanInsertSearchFilterSystem : NpcSearchFilterSystem<StockCanInsert>
{
    [Dependency] private readonly GoapSystem _goap = default!;
    [Dependency] private readonly StockpileSystem _stockpile = default!;

    protected override bool Filter(GoapState state, EntityUid target, StockCanInsert filter)
        => _goap.TryGetValue(state, filter.TargetKey, out var uid)
           && _stockpile.TryGetStock(target, out var stock)
           && _stockpile.CanInsert(stock.Value, uid);
}
