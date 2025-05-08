using System.Threading;
using System.Threading.Tasks;
using Content.Server.NPC;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server.Storage.EntitySystems;
using Content.Shared.Hands.Components;
using Content.Shared.Inventory;
using Content.Shared.Storage;

namespace Content.Server._RF.NPC.HTN.Operators;

/// <summary>
/// Places an entity in the active hand into any available storage in the inventory
/// </summary>
public sealed partial class StorageOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    private InventorySystem _inventory = default!;
    private StorageSystem _storage = default!;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _inventory = sysManager.GetEntitySystem<InventorySystem>();
        _storage = sysManager.GetEntitySystem<StorageSystem>();
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard, CancellationToken cancelToken)
    {
        if (!blackboard.TryGetValue<EntityUid>(NPCBlackboard.Owner, out var owner, _entManager)
            || !blackboard.TryGetValue<Hand>(NPCBlackboard.ActiveHand, out var hand, _entManager)
            || hand.HeldEntity is not { } heldEntity)
            return (false, null);

        foreach (var entity in _inventory.GetHandOrInventoryEntities(owner))
        {
            if (!_entManager.TryGetComponent(entity, out StorageComponent? storage))
                continue;

            if (_storage.Insert(entity, heldEntity, out _, storageComp: storage))
                return (true, new() { {NPCBlackboard.ActiveHandFree, true} });
        }

        return (false, null);
    }
}
