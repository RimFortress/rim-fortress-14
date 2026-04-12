using Content.Server.NPC;
using Content.Shared._RF.Stockpile;
using Robust.Server.Containers;

namespace Content.Server._RF.NPC.Queries.Filters.Stockpile;

public sealed partial class InContainerStockSupplierFilter : RfUtilityQueryFilter
{
    private ContainerSystem _container;
    private ContainerStockSupplierSystem _containerSupplier;

    public override void Initialize(IEntityManager entManager)
    {
        base.Initialize(entManager);
        _container = entManager.System<ContainerSystem>();
        _containerSupplier = entManager.System<ContainerStockSupplierSystem>();
    }

    public override bool Filter(EntityUid uid, NPCBlackboard blackboard)
        => _container.TryGetContainingContainer(uid, out var container)
           && _containerSupplier.InContainer(container.Owner, uid);
}
