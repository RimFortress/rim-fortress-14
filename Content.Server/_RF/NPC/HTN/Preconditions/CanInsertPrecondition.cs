using Content.Server.NPC;
using Content.Server.Storage.EntitySystems;
using Content.Shared.Hands.Components;
using Content.Shared.Inventory;
using Content.Shared.Storage;

namespace Content.Server._RF.NPC.HTN.Preconditions;

/// <summary>
/// Checks if the item in your hand can be placed in the inventory
/// </summary>
public sealed partial class CanInsertPrecondition : InvertiblePrecondition
{
    private InventorySystem _inventory = default!;
    private StorageSystem _storage;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _inventory = sysManager.GetEntitySystem<InventorySystem>();
        _storage = sysManager.GetEntitySystem<StorageSystem>();
    }

    public override bool IsMetInvertible(NPCBlackboard blackboard)
    {
        if (!blackboard.TryGetValue<EntityUid>(NPCBlackboard.Owner, out var owner, EntityManager)
            || !blackboard.TryGetValue<Hand>(NPCBlackboard.ActiveHand, out var hand, EntityManager)
            || hand.HeldEntity is not { } heldEntity)
            return false;

        foreach (var entity in _inventory.GetHandOrInventoryEntities(owner))
        {
            if (!EntityManager.TryGetComponent(entity, out StorageComponent? storage))
                continue;

            if (_storage.CanInsert(entity, heldEntity, out _, storageComp: storage))
                return true;
        }

        return false;
    }
}
