using Content.Server._RF.Stockpile;
using Content.Server.NPC;

namespace Content.Server._RF.NPC.HTN.Preconditions.Stockpile;

/// <summary>
/// Checks if there is an unfilled container in the stockpile where the target entity is stored
/// </summary>
public sealed partial class StockpileFreeContainerPrecondition : InvertiblePrecondition
{
    private StockpileSystem _stockpile = default!;

    /// <summary>
    /// Key containing the target entity
    /// </summary>
    [DataField(required: true)]
    public string TargetKey;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _stockpile = sysManager.GetEntitySystem<StockpileSystem>();
    }

    public override bool IsMetInvertible(NPCBlackboard blackboard)
    {
        return blackboard.TryGetValue(TargetKey, out EntityUid target, EntityManager)
            && _stockpile.TryGetContainingStock(target, out var stock)
            && stock.HasFreeContainer();
    }
}
