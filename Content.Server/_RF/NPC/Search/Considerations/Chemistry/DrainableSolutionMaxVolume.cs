using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared.Chemistry.EntitySystems;

namespace Content.Server._RF.NPC.Search.Considerations.Chemistry;

/// <summary>
/// Evaluates entities with a drainable solution based on the maximum solution volume.
/// </summary>
public sealed partial class DrainableSolutionMaxVolume : BaseSearchConsideration<DrainableSolutionMaxVolume>;

public sealed partial class DrainableSolutionMaxVolumeConsiderationSystem : NpcSearchConsiderationSystem<DrainableSolutionMaxVolume>
{
    [Dependency] private SharedSolutionContainerSystem _solution = default!;

    protected override float GetScore(GoapState state, EntityUid target, DrainableSolutionMaxVolume con)
        => _solution.TryGetDrainableSolution(target, out _, out var sol) ? sol.MaxVolume.Float() : 0f;
}
