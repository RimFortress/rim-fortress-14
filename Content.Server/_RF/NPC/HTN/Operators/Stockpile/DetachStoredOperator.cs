using System.Threading;
using System.Threading.Tasks;
using Content.Server._RF.Stockpile;
using Content.Server.NPC;
using Content.Server.NPC.HTN.PrimitiveTasks;

namespace Content.Server._RF.NPC.HTN.Operators.Stockpile;

public sealed partial class DetachStoredOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    private StockpileSystem _stockpile = default!;

    [DataField(required: true)]
    public string TargetKey;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _stockpile = sysManager.GetEntitySystem<StockpileSystem>();
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard, CancellationToken cancelToken)
    {
        if (!blackboard.TryGetValue(TargetKey, out EntityUid target, _entityManager)
            || !_stockpile.TryGetContainingStock(target, out var stock)
            || !_stockpile.DetachEntity(target, stock))
            return (false, null);

        return (true, null);
    }
}
