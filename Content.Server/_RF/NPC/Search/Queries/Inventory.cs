using Content.Server.Hands.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared.Inventory;

namespace Content.Server._RF.NPC.Search.Queries;

/// <summary>
/// Returns all entities in the agent's inventory.
/// </summary>
public sealed partial class Inventory : BaseSearchQuery<Inventory>;

public sealed class InventoryQuerySystem : NpcSearchQuerySystem<Inventory>
{
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly HandsSystem _hands = default!;

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

        return;

        void RecursiveAdd(EntityUid uid)
        {
            if (!Query.Add(uid))
                return;

            var e = Transform(uid).ChildEnumerator;

            while (e.MoveNext(out var child))
            {
                RecursiveAdd(child);
            }
        }
    }
}
