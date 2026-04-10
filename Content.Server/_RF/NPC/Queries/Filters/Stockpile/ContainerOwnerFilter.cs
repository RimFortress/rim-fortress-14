using Content.Server.NPC;
using Content.Shared.Whitelist;
using Robust.Server.Containers;

namespace Content.Server._RF.NPC.Queries.Filters.Stockpile;

public sealed partial class ContainerOwnerFilter : RfUtilityQueryFilter
{
    private ContainerSystem _container;
    private EntityWhitelistSystem _whitelist;
    private EntityQuery<TransformComponent> _xformQuery;

    [DataField]
    public EntityWhitelist Whitelist = new();

    public override void Initialize(IEntityManager entManager)
    {
        base.Initialize(entManager);
        _container = entManager.System<ContainerSystem>();
        _whitelist = entManager.System<EntityWhitelistSystem>();
        _xformQuery = entManager.GetEntityQuery<TransformComponent>();
    }

    public override bool Filter(EntityUid uid, NPCBlackboard blackboard)
        => _xformQuery.TryComp(uid, out var xform)
           && _container.TryGetOuterContainer(uid, xform, out var container)
           && _whitelist.IsValid(Whitelist, container.Owner);
}
