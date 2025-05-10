using Content.Server.Construction.Components;
using Content.Server.NPC;

namespace Content.Server._RF.NPC.HTN.Preconditions;


public sealed partial class DeconstructionStartedPrecondition : InvertiblePrecondition
{
    private EntityQuery<ConstructionComponent> _constructionQuery;

    [DataField(required: true)]
    public string TargetKey;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _constructionQuery = EntityManager.GetEntityQuery<ConstructionComponent>();
    }

    public override bool IsMetInvertible(NPCBlackboard blackboard)
    {
        return blackboard.TryGetValue(TargetKey, out EntityUid? uid, EntityManager)
               && _constructionQuery.TryComp(uid, out var comp)
               && (comp.TargetNode == comp.DeconstructionNode || comp.Node == comp.DeconstructionNode);
    }
}
