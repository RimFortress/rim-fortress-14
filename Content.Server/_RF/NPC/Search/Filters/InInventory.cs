using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Components;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Robust.Server.Containers;
using Robust.Shared.Containers;

namespace Content.Server._RF.NPC.Search.Filters;

/// <summary>
/// Filters entities in inventory of another entity.
/// </summary>
public sealed partial class InInventory : BaseSearchFilter<InInventory>
{
    /// <summary>
    /// Should the owner's inventory be excluded from the check?
    /// </summary>
    [DataField]
    public bool ExcludeSelf = true;
}

public sealed class InInventoryFilterSystem : NpcSearchFilterSystem<InInventory>
{
    [Dependency] private readonly ContainerSystem _container = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SearchTrackedComponent, GotEquippedEvent>((ent, ref _) => DirtyFilter(ent.Owner));
        SubscribeLocalEvent<SearchTrackedComponent, GotUnequippedEvent>((ent, ref _) => DirtyFilter(ent.Owner));
        SubscribeLocalEvent<EntGotInsertedIntoContainerMessage>(ev => DirtyFilter(ev.Entity));
        SubscribeLocalEvent<EntGotRemovedFromContainerMessage>(ev => DirtyFilter(ev.Entity));
    }

    protected override bool Filter(GoapState state, EntityUid target, InInventory filter)
    {
        if (!_container.TryGetContainingContainer(target, out var container))
            return false;

        if (filter.ExcludeSelf && container.Owner == Goap.GetValue(state, GoapState.Owner))
            return false;

        return _inventory.TryGetSlot(container.Owner, container.ID, out _);
    }
}
