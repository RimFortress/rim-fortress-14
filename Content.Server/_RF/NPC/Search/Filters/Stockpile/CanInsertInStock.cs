using Content.Shared._RF.NPC;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared._RF.Stockpile.Components;
using Content.Shared._RF.Stockpile.Systems;

namespace Content.Server._RF.NPC.Search.Filters.Stockpile;

/// <summary>
/// Filters entities based on whether they can be stored in any of the entity owner's stockpiles.
/// </summary>
// TODO: It's very expensive and must be destroyed
public sealed partial class CanInsertInStock : BaseSearchFilter<CanInsertInStock>;

public sealed class CanInsertInStockSearchFilterSystem : NpcSearchFilterSystem<CanInsertInStock>
{
    [Dependency] private readonly StockpileSystem _stockpile = default!;
    [Dependency] private readonly OwnershipSystem _ownership = default!;

    protected override bool Filter(GoapState state, EntityUid target, CanInsertInStock filter)
    {
        var enumerator = _ownership.GetEntitiesEnumerator<StockpileComponent>(target);
        while (enumerator.MoveNext(out var uid, out var comp))
        {
            if (_stockpile.CanInsert(new(uid, comp), target))
                return true;
        }

        return false;
    }
}
