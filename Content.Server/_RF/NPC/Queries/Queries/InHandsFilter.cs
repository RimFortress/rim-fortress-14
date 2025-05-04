using System.Linq;
using Content.Server.NPC;
using Content.Shared.Hands.Components;
using Robust.Server.Containers;

namespace Content.Server._RF.NPC.Queries.Queries;

/// <summary>
/// Filters entities in someone's hands
/// </summary>
public sealed partial class InHandsFilter : RfUtilityQueryFilter
{
    private ContainerSystem _container = default!;

    private EntityQuery<HandsComponent> _handsQuery;

    /// <summary>
    /// Exclude the hands of the current entity from the check
    /// </summary>
    [DataField]
    public bool ExcludeSelf = true;

    private EntityUid _owner = EntityUid.Invalid;

    public override void Initialize(IEntityManager entManager)
    {
        base.Initialize(entManager);
        _container = entManager.System<ContainerSystem>();

        _handsQuery = entManager.GetEntityQuery<HandsComponent>();
    }

    public override bool Startup(NPCBlackboard blackboard)
    {
        _owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        return true;
    }

    public override bool Filter(EntityUid uid, NPCBlackboard blackboard)
    {
        return _container.TryGetContainingContainer(new(uid, null, null), out var container)
            && _handsQuery.TryComp(container.Owner, out var hands)
            && hands.Hands.Any(x => x.Value.Container == container)
            && (!ExcludeSelf || container.Owner != _owner);
    }
}
