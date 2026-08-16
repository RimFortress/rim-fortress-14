using Content.Server.Hands.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Components;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Robust.Shared.Containers;

namespace Content.Server._RF.NPC.Search.Queries;

/// <summary>
/// Returns all entities in the agent's inventory.
/// </summary>
public sealed partial class Inventory : BaseSearchQuery<Inventory>;

public sealed class InventoryQuerySystem : NpcSearchQuerySystem<Inventory>
{
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly HandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NpcSearcherComponent, DidEquipEvent>(OnSearcherDidEquip);
        SubscribeLocalEvent<NpcSearcherComponent, DidUnequipEvent>(OnSearcherDidUnequip);
        SubscribeLocalEvent<SearchTrackedComponent, EntInsertedIntoContainerMessage>(OnTrackedInserted);
        SubscribeLocalEvent<SearchTrackedComponent, EntRemovedFromContainerMessage>(OnTrackedRemoved);
    }

    private void OnSearcherDidEquip(Entity<NpcSearcherComponent> ent, ref DidEquipEvent ev)
    {
        Query.Clear();
        RecursiveAdd(ev.Equipment);

        foreach (var (proto, _) in ent.Comp.Queries)
        {
            if (QueryTypeIs(proto))
                Searcher.ReportDirty(ent, proto, added: Query);
        }

        Query.Clear();
    }

    private void OnSearcherDidUnequip(Entity<NpcSearcherComponent> ent, ref DidUnequipEvent ev)
    {
        Query.Clear();
        RecursiveAdd(ev.Equipment);

        foreach (var (proto, _) in ent.Comp.Queries)
        {
            if (QueryTypeIs(proto))
                Searcher.ReportDirty(ent, proto, removed: Query);
        }

        Query.Clear();
    }

    private void OnTrackedInserted(Entity<SearchTrackedComponent> ent, ref EntInsertedIntoContainerMessage ev)
    {
        if (!HasComp<InventoryComponent>(ent)
            || !_container.TryGetOuterContainer(ent, Transform(ev.Entity), out var container)
            || !HasComp<InventoryComponent>(container.Owner))
            return;

        foreach (var ((agent, proto), _) in ent.Comp.Tracking)
        {
            if (!QueryTypeIs(proto))
                continue;

            Query.Clear();
            RecursiveAdd(ev.Entity);
            Searcher.ReportDirty(agent, proto, added: Query);
        }

        Query.Clear();
    }

    private void OnTrackedRemoved(Entity<SearchTrackedComponent> ent, ref EntRemovedFromContainerMessage ev)
    {
        if (!HasComp<InventoryComponent>(ent)
            || !_container.TryGetOuterContainer(ev.Entity, Transform(ev.Entity), out var container)
            || !HasComp<InventoryComponent>(container.Owner))
            return;

        foreach (var ((agent, proto), _) in ent.Comp.Tracking)
        {
            if (!QueryTypeIs(proto))
                continue;

            Query.Clear();
            RecursiveAdd(ev.Entity);
            Searcher.ReportDirty(agent, proto, removed: Query);
        }

        Query.Clear();
    }

    protected override void GetQuery(GoapState state, Inventory query)
    {
        var owner = state.GetValue(GoapState.Owner);

        if (!_inventory.TryGetContainerSlotEnumerator(owner, out var enumerator))
            return;

        while (enumerator.MoveNext(out var slot))
        {
            foreach (var child in slot.ContainedEntities)
            {
                RecursiveAdd(child);
            }
        }

        foreach (var child in _hands.EnumerateHeld(owner))
        {
            RecursiveAdd(child);
        }
    }

    private void RecursiveAdd(EntityUid uid)
    {
        if (!Query.Add(uid))
            return;

        var enumerator = Transform(uid).ChildEnumerator;
        while (enumerator.MoveNext(out var child))
        {
            RecursiveAdd(child);
        }
    }
}
