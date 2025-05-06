using Content.Server.Construction.Components;
using Content.Server.NPC;

namespace Content.Server._RF.NPC.Queries.Queries;

/// <summary>
/// Filters completed constructions
/// </summary>
public sealed partial class ConstructionFinishedFilter : RfUtilityQueryFilter
{
    private EntityQuery<ConstructionComponent> _constructionQuery;

    public override void Initialize(IEntityManager entManager)
    {
        base.Initialize(entManager);
        _constructionQuery = entManager.GetEntityQuery<ConstructionComponent>();
    }

    public override bool Filter(EntityUid uid, NPCBlackboard blackboard)
    {
        return _constructionQuery.TryComp(uid, out var comp) && string.IsNullOrEmpty(comp.TargetNode);
    }
}
