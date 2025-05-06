using System.Linq;
using Content.Server.NPC;
using Content.Shared.Hands.Components;
using Content.Shared.Inventory;
using Robust.Server.Containers;

namespace Content.Server._RF.NPC.HTN.Preconditions;

/// <summary>
/// Checks if the target entity is in someone's inventory
/// </summary>
public sealed partial class InInventoryPrecondition : InvertiblePrecondition
{
    private InventorySystem _inventory = default!;
    private ContainerSystem _container = default!;

    [DataField(required: true)]
    public string TargetKey = default!;

    /// <summary>
    /// Exclude the inventory of the current entity from the check
    /// </summary>
    [DataField]
    public bool ExcludeSelf = true;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _inventory = sysManager.GetEntitySystem<InventorySystem>();
        _container = sysManager.GetEntitySystem<ContainerSystem>();
    }

    public override bool IsMetInvertible(NPCBlackboard blackboard)
    {
        if (!blackboard.TryGetValue(TargetKey, out EntityUid? target, EntityManager)
            || !blackboard.TryGetValue(NPCBlackboard.Owner, out EntityUid? owner, EntityManager))
            return false;

        if (ExcludeSelf && _inventory.GetHandOrInventoryEntities(owner.Value).ToList().Contains(target.Value))
            return false;

        if (_container.TryGetContainingContainer(new(target.Value, null, null), out var container)
            && EntityManager.TryGetComponent(container.Owner, out HandsComponent? hands)
            && hands.Hands.Any(x => x.Value.Container == container)
            && (!ExcludeSelf || container.Owner != owner))
            return true;

        if (_inventory.TryGetContainingSlot(target.Value, out _))
            return true;

        return false;
    }
}
