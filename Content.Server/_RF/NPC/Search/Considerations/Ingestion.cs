using Content.Server.Nutrition.Components;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Clothing;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Robust.Shared.Utility;

namespace Content.Server._RF.NPC.Search.Considerations;


public sealed partial class Ingestion : BaseSearchConsideration<Ingestion>
{
    /// <summary>
    /// Returns the total nutrition of the target entity.
    /// </summary>
    [DataField]
    public bool Nutrition;

    /// <summary>
    /// Gets the total metabolizable hydration from an entity.
    /// </summary>
    [DataField]
    public bool Hydration;
}

public sealed partial class IngestionSearchConsiderationSystem : NpcSearchConsiderationSystem<Ingestion>
{
    [Dependency] private IngestionSystem _ingestion = default!;
    [Dependency] private readonly EntityQuery<BadFoodComponent> _badQuery = default!;
    [Dependency] private readonly EntityQuery<IgnoreBadFoodComponent> _ignoreQuery = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeRescoreEvent<EdibleComponent, SolutionTransferredEvent>();
        SubscribeRescoreEvent<ItemMaskToggledEvent>();
    }

    protected override float GetScore(GoapState state, EntityUid target, Ingestion con)
    {
        DebugTools.Assert(con.Hydration != con.Nutrition);
        var owner = Goap.GetValue(state, GoapState.Owner);

        if (!_ingestion.HasMouthAvailable(owner, target))
            return 0f;

        if (!_ignoreQuery.HasComp(owner) && _badQuery.HasComp(target))
            return 0f;

        if (con.Hydration)
            return _ingestion.TotalHydration(owner, target);

        if (con.Nutrition)
            return _ingestion.TotalNutrition(target, owner);

        return 0f;
    }
}
