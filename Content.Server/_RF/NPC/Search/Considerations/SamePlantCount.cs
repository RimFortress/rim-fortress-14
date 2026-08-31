using Content.Server.Botany.Components;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared._RF.NPC.Systems;

namespace Content.Server._RF.NPC.Search.Considerations;

/// <summary>
/// Returns the number of seeds of the same plant that was planted in the target entity.
/// </summary>
public sealed partial class SamePlantCount : BaseSearchConsideration<SamePlantCount>;

public sealed class SamePlantCountConsiderationSystem : NpcSearchConsiderationSystem<SamePlantCount>
{
    [Dependency] private readonly OwnershipSystem _ownership = default!;
    [Dependency] private readonly EntityQuery<PlantHolderComponent> _query = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeRescoreEvent<SeedComponent, MapInitEvent>();
    }

    protected override float GetScore(GoapState state, EntityUid target, SamePlantCount con)
    {
        if (!_query.TryComp(target, out var comp) || comp.Seed == null)
            return 0f;

        var count = 0;
        var seeds = _ownership.GetEntitiesEnumerator<SeedComponent>(target);
        while (seeds.MoveNext(out var uid, out var seed))
        {
            if (uid != target && seed.Seed == comp.Seed)
                count++;
        }

        return count;
    }
}
