using Content.Server._RF.NPC.GOAP.Systems;
using Content.Server.Storage.EntitySystems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared.Inventory;
using Content.Shared.Storage;

namespace Content.Server._RF.NPC.GOAP.Actions.Interaction;

/// <summary>
/// Places an entity in the active hand into any available storage in the inventory.
/// </summary>
public sealed partial class Storage : BaseGoapAction<Storage>;

public sealed class StorageActionSystem : GoapActionSystem<Storage>
{
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly StorageSystem _storage = default!;

    protected override bool ActionStartup(Entity<GoapComponent> ent, Storage action)
    {
        if (TryGetValue(ent, action, GoapState.ActiveHandEntity, out var heldEntity))
            return false;

        foreach (var entity in _inventory.GetHandOrInventoryEntities(ent.Owner))
        {
            if (TryComp(entity, out StorageComponent? storage)
                && _storage.Insert(entity, heldEntity, out _, storageComp: storage))
                return true;
        }

        return false;
    }
}
