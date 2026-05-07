using Content.Server.Nutrition.Components;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared.Nutrition.EntitySystems;

namespace Content.Server._RF.NPC.Search.Considerations;

/// <summary>
/// Gets the total metabolizable hydration from an entity.
/// </summary>
public sealed partial class TotalHydration : BaseSearchConsideration<TotalHydration>;

public sealed class TotalHydrationSystem : NpcSearchConsiderationSystem<TotalHydration>
{
    [Dependency] private readonly IngestionSystem _ingestion = default!;
    [Dependency] private readonly EntityQuery<BadFoodComponent> _badQuery;

    protected override float GetScore(GoapState state, EntityUid target, TotalHydration con)
    {
        var owner = state.GetValue(GoapState.Owner);

        if (!_ingestion.HasMouthAvailable(owner, target)
            || _badQuery.HasComp(target))
            return 0f;

        return _ingestion.TotalHydration(owner, target);
    }
}
