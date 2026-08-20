using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared._RF.Workshops;
using Content.Shared._RF.Workshops.Systems;

namespace Content.Server._RF.NPC.Search.Filters.Workshop;

/// <summary>
/// Filters workshops where craft can be started right away.
/// </summary>
public sealed partial class WorkshopCanStartCraft : BaseSearchFilter<WorkshopCanStartCraft>;

public sealed class WorkshopCanStartCraftSearchFilterSystem : NpcSearchFilterSystem<WorkshopCanStartCraft>
{
    [Dependency] private readonly WorkshopSystem _workshop = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WorkshopQueueAdded>((ref ev) => DirtyFilter(ev.Workshop));
        SubscribeLocalEvent<WorkshopQueueRemoved>((ref ev) => DirtyFilter(ev.Workshop));
        SubscribeLocalEvent<WorkshopRecipeSuspend>((ref ev) => DirtyFilter(ev.Workshop));
        SubscribeLocalEvent<WorkshopIngredientInserted>((ref ev) => DirtyFilter(ev.Workshop));
        SubscribeLocalEvent<WorkshopIngredientRemoved>((ref ev) => DirtyFilter(ev.Workshop));
    }

    protected override bool Filter(GoapState state, EntityUid target, WorkshopCanStartCraft filter)
        => _workshop.CanStartCraft(target);
}
