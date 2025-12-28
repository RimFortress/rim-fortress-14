using Content.Server.NPC;
using Robust.Server.GameObjects;

namespace Content.Server._RF.NPC.Queries.Considerations;

public sealed partial class NormTargetDistanceCon : RfUtilityConsideration
{
    private TransformSystem _transform;
    private EntityQuery<TransformComponent> _query;

    public override void Initialize()
    {
        base.Initialize();
        _transform = Entity.System<TransformSystem>();
        _query = Entity.GetEntityQuery<TransformComponent>();
    }

    public override float GetScore(NPCBlackboard blackboard, EntityUid targetUid)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_query.TryComp(targetUid, out var targetXform) || !_query.TryComp(owner, out var xform))
            return 0f;

        if (!targetXform.Coordinates.TryDistance(Entity, _transform, xform.Coordinates, out var distance))
            return 0f;

        return 1f - 1f / distance;
    }
}
