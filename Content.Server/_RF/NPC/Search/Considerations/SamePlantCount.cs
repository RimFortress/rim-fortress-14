using Content.Server.Botany;
using Content.Server.Botany.Components;
using Content.Shared._RF.NPC;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Systems;
using Robust.Shared.Timing;

namespace Content.Server._RF.NPC.Search.Considerations;

/// <summary>
/// Returns the number of seeds of the same plant that was planted in the target entity.
/// </summary>
public sealed partial class SamePlantCount : BaseSearchConsideration<SamePlantCount>;

public sealed class SamePlantCountConsiderationSystem : NpcSearchConsiderationSystem<SamePlantCount>
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly OwnershipSystem _ownership = default!;
    [Dependency] private readonly EntityQuery<PlantHolderComponent> _query = default!;

    private GameTick _cacheTick;
    private readonly Dictionary<SeedData, int> _cache = new();

    protected override float GetScore(GoapState state, EntityUid target, SamePlantCount con)
    {
        if (!_query.TryComp(target, out var comp) || comp.Seed == null)
            return 0f;

        if (_cacheTick != _timing.CurTick)
        {
            _cacheTick = _timing.CurTick;
            _cache.Clear();
        }

        if (_cache.TryGetValue(comp.Seed, out var count))
            return count;

        count = 0;
        var seeds = _ownership.GetEntitiesEnumerator<SeedComponent>(target);
        while (seeds.MoveNext(out var uid, out var seed))
        {
            if (uid != target && seed.Seed == comp.Seed)
                count++;
        }

        _cache[comp.Seed] = count;
        return count;
    }
}
