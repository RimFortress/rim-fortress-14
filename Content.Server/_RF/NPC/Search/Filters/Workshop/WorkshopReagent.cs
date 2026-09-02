using Content.Server.Item;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared._RF.Workshops.Components;
using Content.Shared._RF.Workshops.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Item;

namespace Content.Server._RF.NPC.Search.Filters.Workshop;

/// <summary>
/// Filters entities based on whether they can be used
/// as reagent ingredients for a current recipe in the target workshop.
/// </summary>
public sealed partial class WorkshopReagent : BaseSearchFilter<WorkshopReagent>
{
    /// <summary>
    /// Target workshop entity.
    /// </summary>
    [DataField(required: true)]
    public StateKey<EntityUid> TargetKey;
}

public sealed partial class WorkshopReagentSearchFilterSystem : NpcSearchGoapKeyFilterSystem<WorkshopReagent, EntityUid>
{
    [Dependency] private ItemSystem _item = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private WorkshopSystem _workshop = default!;
    [Dependency] private readonly EntityQuery<WorkshopComponent> _workshopQuery = default!;
    [Dependency] private readonly EntityQuery<ItemComponent> _itemQuery = default!;

    protected override HashSet<StateKey<EntityUid>> GetSubscribeKeys(WorkshopReagent filter)
        => new() { filter.TargetKey };

    protected override bool Filter(GoapState state, EntityUid target, WorkshopReagent filter)
    {
        if (!Goap.TryGetValue(state, filter.TargetKey, out var workshop)
            || !_workshopQuery.TryComp(workshop, out var comp)
            || !_solution.TryGetDrainableSolution(target, out _, out var sol))
            return false;

        if (!_itemQuery.TryComp(target, out var item)
            || _item.GetSizePrototype(item.Size) > _item.GetSizePrototype(comp.MaxItemSize))
            return false;

        var ent = new Entity<WorkshopComponent?>(workshop, comp);

        if (_workshop.GetCurrentRecipe(ent) is not { } recipe)
            return false;

        var remining = _workshop.GetRemainingIngredients(ent, recipe);

        if (remining.Reagents.Count == 0)
            return false;

        foreach (var (reagent, _) in remining.Reagents)
        {
            if (sol.GetTotalPrototypeQuantity(reagent) > 0)
                return true;
        }

        return false;
    }
}
