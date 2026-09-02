using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared._RF.Workshops;
using Content.Shared._RF.Workshops.Components;
using Content.Shared._RF.Workshops.Systems;

namespace Content.Server._RF.NPC.Search.Filters.Workshop;

/// <summary>
/// Filters workshops that have an active recipe for crafting.
/// </summary>
public sealed partial class WorkshopActiveRecipe : BaseSearchFilter<WorkshopActiveRecipe>;

public sealed partial class WorkshopActiveRecipeSearchFilterSystem : NpcSearchFilterSystem<WorkshopActiveRecipe>
{
    [Dependency] private WorkshopSystem _workshop = default!;
    [Dependency] private readonly EntityQuery<WorkshopComponent> _query = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WorkshopQueueAdded>((ref ev) => DirtyFilter(ev.Workshop));
        SubscribeLocalEvent<WorkshopQueueRemoved>((ref ev) => DirtyFilter(ev.Workshop));
        SubscribeLocalEvent<WorkshopRecipeSuspend>((ref ev) => DirtyFilter(ev.Workshop));
    }

    protected override bool Filter(GoapState state, EntityUid target, WorkshopActiveRecipe filter)
        => _query.TryComp(target, out var comp) && _workshop.GetCurrentRecipe(new(target, comp)) != null;
}
