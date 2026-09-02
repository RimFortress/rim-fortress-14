using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared.Chemistry.EntitySystems;

namespace Content.Server._RF.NPC.Search.Considerations.Chemistry;

/// <summary>
/// Evaluates entities with a drainable solution based on the solution volume.
/// </summary>
public sealed partial class DrainableSolutionVolume : BaseSearchConsideration<DrainableSolutionVolume>;

public sealed partial class DrainableSolutionVolumeConsiderationSystem : NpcSearchConsiderationSystem<DrainableSolutionVolume>
{
    [Dependency] private SharedSolutionContainerSystem _solution = default!;

    protected override float GetScore(GoapState state, EntityUid target, DrainableSolutionVolume con)
        => _solution.TryGetDrainableSolution(target, out _, out var sol) ? sol.Volume.Float() : 0f;
}
