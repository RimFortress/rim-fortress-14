using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared._RF.NPC.Systems;
using Content.Shared.Botany.Items.Components;
using Content.Shared.Botany.Systems;

namespace Content.Server._RF.NPC.Search.Considerations;

/// <summary>
/// Returns the number of seeds of the same plant that was planted in the target entity.
/// </summary>
public sealed partial class SamePlantCount : BaseSearchConsideration<SamePlantCount>;

public sealed partial class SamePlantCountConsiderationSystem : NpcSearchConsiderationSystem<SamePlantCount>
{
    [Dependency] private OwnershipSystem _ownership = default!;
    [Dependency] private PlantTraySystem _plantTray = default!;
    [Dependency] private readonly EntityQuery<SeedComponent> _seedQuery = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeRescoreEvent<SeedComponent, MapInitEvent>();
    }

    protected override float GetScore(GoapState state, EntityUid target, SamePlantCount con)
    {
        if (!_plantTray.TryGetPlant(target, out var plant)
            || !_seedQuery.TryComp(plant.Value, out var seed))
            return 0f;

        var count = 0;
        var seeds = _ownership.GetEntitiesEnumerator<SeedComponent>(target);
        while (seeds.MoveNext(out var uid, out var comp))
        {
            if (uid != target && seed.PlantProtoId == comp.PlantProtoId)
                count++;
        }

        return count;
    }
}
