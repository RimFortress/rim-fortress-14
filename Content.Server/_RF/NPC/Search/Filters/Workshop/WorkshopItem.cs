using Content.Server.Item;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared._RF.Workshops.Components;
using Content.Shared._RF.Workshops.Systems;
using Content.Shared.Item;

namespace Content.Server._RF.NPC.Search.Filters.Workshop;

/// <summary>
/// Filters entities based on whether they can be used as item
/// ingredient for a current recipe in the target workshop.
/// </summary>
public sealed partial class WorkshopItem : BaseSearchFilter<WorkshopItem>
{
    /// <summary>
    /// Target workshop entity.
    /// </summary>
    [DataField(required: true)]
    public StateKey<EntityUid> TargetKey;
}

public sealed partial class WorkshopItemSearchFilterSystem : NpcSearchGoapKeyFilterSystem<WorkshopItem, EntityUid>
{
    [Dependency] private ItemSystem _item = default!;
    [Dependency] private WorkshopSystem _workshop = default!;
    [Dependency] private EntityQuery<WorkshopComponent> _workshopQuery;
    [Dependency] private EntityQuery<ItemComponent> _itemQuery;

    protected override HashSet<StateKey<EntityUid>> GetSubscribeKeys(WorkshopItem filter)
        => new() { filter.TargetKey };

    protected override bool Filter(GoapState state, EntityUid target, WorkshopItem filter)
    {
        if (!Goap.TryGetValue(state, filter.TargetKey, out var workshop)
            || !_workshopQuery.TryComp(workshop, out var comp)
            || Prototype(target) is not { } proto)
            return false;

        if (!_itemQuery.TryComp(target, out var item)
            || _item.GetSizePrototype(item.Size) > _item.GetSizePrototype(comp.MaxItemSize))
            return false;

        var ent = new Entity<WorkshopComponent?>(workshop, comp);

        if (_workshop.GetCurrentRecipe(ent) is not { } recipe)
            return false;

        var remining = _workshop.GetRemainingIngredients(ent, recipe);

        if (remining.Items.Count == 0)
            return false;

        foreach (var (entProtoId, _) in remining.Items)
        {
            if (proto == entProtoId)
                return true;
        }

        return false;
    }
}
