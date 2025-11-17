using Content.Server.Construction.Components;
using Content.Server.NPC;
using Content.Shared._RF.Construction;

namespace Content.Server._RF.NPC.Queries.Filters;

/// <summary>
/// Filters completed constructions
/// </summary>
public sealed partial class ConstructionFinishedFilter : RfUtilityQueryFilter
{
    private EntityQuery<ConstructionComponent> _constructionQuery;
    private EntityQuery<CommonConstructionGhostComponent> _ghostQuery;

    public override void Initialize(IEntityManager entManager)
    {
        base.Initialize(entManager);
        _constructionQuery = entManager.GetEntityQuery<ConstructionComponent>();
        _ghostQuery = entManager.GetEntityQuery<CommonConstructionGhostComponent>();
    }

    public override bool Filter(EntityUid uid, NPCBlackboard blackboard)
    {
        return !_ghostQuery.HasComp(uid)
               && _constructionQuery.TryComp(uid, out var comp)
               && string.IsNullOrEmpty(comp.TargetNode);
    }
}
