using System.Threading;
using System.Threading.Tasks;
using Content.Server.Botany.Components;
using Content.Server.NPC;
using Content.Server.NPC.HTN.PrimitiveTasks;

namespace Content.Server._RF.NPC.HTN.Operators;

public sealed partial class GetHolderSeedOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    [DataField(required: true)]
    public string TargetKey;

    [DataField(required: true)]
    public string SeedKey;

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard, CancellationToken cancelToken)
    {
        if (!blackboard.TryGetValue(TargetKey, out EntityUid? uid, _entityManager)
            || !_entityManager.TryGetComponent(uid, out PlantHolderComponent? comp)
            || comp.Seed == null)
            return (false, null);

        return (true, new() { {SeedKey, comp.Seed} });
    }
}
