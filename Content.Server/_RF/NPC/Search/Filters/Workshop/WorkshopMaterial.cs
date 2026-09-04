using Content.Server.Item;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared._RF.Workshops.Components;
using Content.Shared._RF.Workshops.Systems;
using Content.Shared.Item;
using Content.Shared.Stacks;

namespace Content.Server._RF.NPC.Search.Filters.Workshop;

/// <summary>
/// Filters entities based on whether they can be used
/// as material ingredients for a current recipe in the target workshop.
/// </summary>
public sealed partial class WorkshopMaterial : BaseSearchFilter<WorkshopMaterial>
{
    /// <summary>
    /// Target workshop entity.
    /// </summary>
    [DataField(required: true)]
    public StateKey<EntityUid> TargetKey;
}

public sealed partial class WorkshopMaterialSearchFilterSystem : NpcSearchGoapKeyFilterSystem<WorkshopMaterial, EntityUid>
{
    [Dependency] private ItemSystem _item = default!;
    [Dependency] private WorkshopSystem _workshop = default!;
    [Dependency] private EntityQuery<WorkshopComponent> _workshopQuery;
    [Dependency] private EntityQuery<StackComponent> _stackQuery;
    [Dependency] private EntityQuery<ItemComponent> _itemQuery;

    protected override HashSet<StateKey<EntityUid>> GetSubscribeKeys(WorkshopMaterial filter)
        => new() { filter.TargetKey };

    protected override bool Filter(GoapState state, EntityUid target, WorkshopMaterial filter)
    {
        if (!Goap.TryGetValue(state, filter.TargetKey, out var workshop)
            || !_workshopQuery.TryComp(workshop, out var comp)
            || !_stackQuery.TryComp(target, out var stack))
            return false;

        if (!_itemQuery.TryComp(target, out var item)
            || _item.GetSizePrototype(item.Size) > _item.GetSizePrototype(comp.MaxItemSize))
            return false;

        var ent = new Entity<WorkshopComponent?>(workshop, comp);

        if (_workshop.GetCurrentRecipe(ent) is not { } recipe)
            return false;

        var remining = _workshop.GetRemainingIngredients(ent, recipe);

        if (remining.Materials.Count == 0)
            return false;

        foreach (var (stackId, _) in remining.Materials)
        {
            if (stack.StackTypeId == stackId)
                return true;
        }

        return false;
    }
}
