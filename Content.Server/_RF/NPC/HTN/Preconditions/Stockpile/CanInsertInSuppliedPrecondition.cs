using Content.Server._RF.Stockpile;
using Content.Server.NPC;

namespace Content.Server._RF.NPC.HTN.Preconditions.Stockpile;

/// <summary>
/// Checks if the target entity can be stocked to at least one
/// stockpile supplied from the stockpile where it is currently located
/// </summary>
public sealed partial class CanInsertInSuppliedPrecondition : InvertiblePrecondition
{
    private StockpileSystem _stockpile = default!;

    [DataField(required: true)]
    public string TargetKey;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _stockpile = sysManager.GetEntitySystem<StockpileSystem>();
    }

    public override bool IsMetInvertible(NPCBlackboard blackboard)
    {
        if (!blackboard.TryGetValue(TargetKey, out EntityUid target, EntityManager)
            || !_stockpile.TryGetStock(target, out var stock))
            return false;

        foreach (var id in stock.SuppliedStockpiles)
        {
            if (_stockpile.TryGetStock(id, out var supplied)
                && _stockpile.CanInsert(supplied, target))
                return true;
        }

        return false;
    }
}
