using System.Threading;
using System.Threading.Tasks;
using Content.Server.Construction;
using Content.Server.Construction.Components;
using Content.Server.NPC;
using Content.Server.NPC.HTN.PrimitiveTasks;

namespace Content.Server._RF.NPC.HTN.Operators;


public sealed partial class StartDeconstructionOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    private ConstructionSystem _construction = default!;

    private EntityQuery<ConstructionComponent> _constructionQuery;

    [DataField(required: true)]
    public string TargetKey;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _construction = sysManager.GetEntitySystem<ConstructionSystem>();
        _constructionQuery = _entityManager.GetEntityQuery<ConstructionComponent>();
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard, CancellationToken cancelToken)
    {
        if (!blackboard.TryGetValue(TargetKey, out EntityUid? uid, _entityManager)
            || !_constructionQuery.TryComp(uid, out var comp))
            return (false, null);

        if (comp.TargetNode == comp.DeconstructionNode
            || comp.Node == comp.DeconstructionNode)
            return (true, null);

        if (!_construction.SetPathfindingTarget(uid.Value, comp.DeconstructionNode, comp))
            return (false, null);

        return (true, null);
    }
}
