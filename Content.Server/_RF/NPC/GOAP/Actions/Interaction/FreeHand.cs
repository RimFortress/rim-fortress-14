using Content.Server.Hands.Systems;
using Content.Server.Storage.EntitySystems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared.Inventory;
using Content.Shared.Storage;

namespace Content.Server._RF.NPC.GOAP.Actions.Interaction;

/// <summary>
/// Frees the active hand of the agent: it switches to a hand that is free, or puts what it holds into a storage it has
/// on it, a backpack or a pocket, or, if there is nowhere to put it, drops it.
/// </summary>
public sealed partial class FreeHand : BaseGoapAction<FreeHand>;

public sealed partial class FreeHandSystem : GoapActionSystem<FreeHand>
{
    [Dependency] private HandsSystem _hands = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private StorageSystem _storage = default!;

    // More than switching the hands, which is free, and less than dropping what is held.
    protected override float ActionCost(Entity<GoapComponent> ent, GoapState state, FreeHand action) => 1f;

    protected override bool ActionStartup(Entity<GoapComponent> ent, FreeHand action)
    {
        if (_hands.ActiveHandIsEmpty(ent.Owner))
            return true;

        if (_hands.TryGetEmptyHand(ent.Owner, out var hand))
        {
            _hands.SetActiveHand(ent.Owner, hand);
            return true;
        }

        if (_hands.GetActiveItem(ent.Owner) is not { } held)
            return true;

        foreach (var entity in _inventory.GetHandOrInventoryEntities(ent.Owner))
        {
            if (entity != held
                && TryComp(entity, out StorageComponent? storage)
                && _storage.Insert(entity, held, out _, storageComp: storage))
            {
                CreateDump($"put {ToPrettyString(held)} into {ToPrettyString(entity)}");
                return true;
            }
        }

        CreateDump($"nowhere to put {ToPrettyString(held)}, dropped");
        return _hands.TryDrop(ent.Owner);
    }
}
