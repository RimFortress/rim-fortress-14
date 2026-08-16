using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Systems;
using Robust.Server.GameObjects;

namespace Content.Server._RF.NPC.Search.Considerations;

/// <summary>
/// Returns the distance between the agent and the target entity.
/// </summary>
public sealed partial class TargetDistance : BaseSearchConsideration<TargetDistance>;

public sealed class TargetDistanceConsiderationSystem : NpcSearchConsiderationSystem<TargetDistance>
{
    [Dependency] private readonly TransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeRescoreEvent<MoveEvent>();
    }

    protected override float GetScore(GoapState state, EntityUid target, TargetDistance con)
    {
        if (!EntityManager.TransformQuery.TryComp(target, out var xform))
            return 0f;

        var coords = Transform(state.GetValue(GoapState.Owner)).Coordinates;

        if (!xform.Coordinates.TryDistance(EntityManager, _transform, coords, out var distance))
            return 0f;

        return distance;
    }
}
