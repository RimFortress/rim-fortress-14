using Content.Server._RF.Stockpile;
using Content.Server.NPC;

namespace Content.Server._RF.NPC.HTN.Preconditions.Stockpile;

/// <summary>
/// Checks if the entity is in any stockpile
/// </summary>
public sealed partial class StoredPrecondition : InvertiblePrecondition
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
        if (!blackboard.TryGetValue(TargetKey, out EntityUid? uid, EntityManager))
            return false;

        foreach (var stock in _stockpile.AllStockpiles())
        {
            if (stock.ContainsEntity(uid.Value))
                return true;
        }

        return false;
    }
}
