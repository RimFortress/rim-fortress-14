using Content.Server._RF.NPC.GOAP.Systems;
using Content.Server.Storage.EntitySystems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared.Inventory;
using Content.Shared.Storage;

namespace Content.Server._RF.NPC.GOAP.Conditions.Interaction;

/// <summary>
/// Checks if the item in agent's hand can be placed in the inventory.
/// </summary>
public sealed partial class CanInsert : BaseGoapCondition<CanInsert>;

public sealed class CanInsertSystem : GoapConditionSystem<CanInsert>
{
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly StorageSystem _storage = default!;
    [Dependency] private readonly EntityQuery<StorageComponent> _storageQuery = default!;

    protected override bool ConditionCheck(EntityUid uid, GoapState state, CanInsert condition)
    {
        if (!TryGetValue(state, condition, GoapState.ActiveHandEntity, out var heldEntity))
            return false;

        foreach (var invEnt in _inventory.GetHandOrInventoryEntities(uid))
        {
            if (_storageQuery.TryComp(invEnt, out var storage)
                && _storage.CanInsert(invEnt, heldEntity, out _, storageComp: storage))
                return true;
        }

        return false;
    }
}
