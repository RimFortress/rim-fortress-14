using Content.Server.NPC;
using Content.Shared.Inventory;

namespace Content.Server._RF.NPC.Queries.Queries;

/// <summary>
/// Filters entities in the inventory
/// </summary>
public sealed partial class InventoryFilter : RfUtilityQueryFilter
{
    private InventorySystem _inventory = default!;

    public override void Initialize(IEntityManager entManager)
    {
        base.Initialize(entManager);
        _inventory = entManager.System<InventorySystem>();
    }

    public override bool Filter(EntityUid uid, NPCBlackboard blackboard)
    {
        return _inventory.TryGetContainingSlot(uid, out _);
    }
}
