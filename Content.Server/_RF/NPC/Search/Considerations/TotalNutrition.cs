using Content.Server.Nutrition.Components;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared.Nutrition.EntitySystems;

namespace Content.Server._RF.NPC.Search.Considerations;

/// <summary>
/// Returns the total nutrition of the target entity.
/// </summary>
public sealed partial class TotalNutrition : BaseSearchConsideration<TotalNutrition>;

public sealed class TotalNutritionSystem : NpcSearchConsiderationSystem<TotalNutrition>
{
    [Dependency] private readonly IngestionSystem _ingestion = default!;
    [Dependency] private readonly EntityQuery<IgnoreBadFoodComponent> _ignoreQuery = default!;
    [Dependency] private readonly EntityQuery<BadFoodComponent> _badFoodQuery = default!;

    protected override float GetScore(GoapState state, EntityUid target, TotalNutrition con)
    {
        var owner = state.GetValue(GoapState.Owner);

        if (!_ingestion.HasMouthAvailable(owner, target))
            return 0f;

        // no mouse don't eat the uranium-235
        if (!_ignoreQuery.HasComp(owner) && _badFoodQuery.HasComp(target))
            return 0f;

        return _ingestion.TotalNutrition(target, owner);
    }
}
