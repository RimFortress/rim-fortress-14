using Content.Server.Botany.Components;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Systems;

namespace Content.Server._RF.NPC.Search.Considerations;

/// <summary>
/// Evaluates plant holders based on the amount of water they contain.
/// </summary>
public sealed partial class PlantHolderWater : BaseSearchConsideration<PlantHolderWater>;

public sealed class PlantHolderWaterConsiderationSystem : NpcSearchConsiderationSystem<PlantHolderWater>
{
    [Dependency] private readonly EntityQuery<PlantHolderComponent> _query = default!;

    protected override float GetScore(GoapState state, EntityUid target, PlantHolderWater con)
    {
        if (!_query.TryComp(target, out var comp))
            return 0;

        return comp.WaterLevel / 100;
    }
}
