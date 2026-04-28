using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Systems;
using Robust.Server.GameObjects;

namespace Content.Server._RF.NPC.Search.Considerations;

/// <summary>
/// Returns the distance between the agent and the target entity.
/// </summary>
public sealed partial class TargetDistance : BaseSearchConsideration<TargetDistance>;

public sealed class TargeDistanceConSystem : NpcSearchConsiderationSystem<TargetDistance>
{
    [Dependency] private readonly TransformSystem _transform = default!;
    protected override float GetScore(GoapState state, EntityUid target, TargetDistance con)
    {
        var coords = Transform(state.GetValue(GoapState.Owner)).Coordinates;
        var targetCoords = Transform(target).Coordinates;

        if (!targetCoords.TryDistance(EntityManager, _transform, coords, out var distance))
            return 0f;

        return distance;
    }
}
