using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared.Whitelist;
using Robust.Server.Containers;
using Robust.Shared.Containers;

namespace Content.Server._RF.NPC.Search.Filters;

/// <summary>
/// Filters entities based on the container in which they are located.
/// </summary>
public sealed partial class ContainingContainer : BaseSearchFilter<ContainingContainer>
{
    /// <summary>
    /// Container entity whitelist.
    /// </summary>
    [DataField]
    public EntityWhitelist? Whitelist;

    /// <summary>
    /// Container entity blacklist.
    /// </summary>
    [DataField]
    public EntityWhitelist? Blacklist;

    /// <summary>
    /// If true, the filter will check the outermost container that contains the entity.
    /// </summary>
    [DataField]
    public bool OuterContainer;

    /// <summary>
    /// The ID that must be associated with the container in which the entity is located.
    /// </summary>
    [DataField]
    public string? ContainerId;
}

public sealed partial class ContainingContainerSearchFilterSystem : NpcSearchFilterSystem<ContainingContainer>
{
    [Dependency] private ContainerSystem _container = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EntGotInsertedIntoContainerMessage>(ev => DirtyFilter(ev.Entity));
        SubscribeLocalEvent<EntGotRemovedFromContainerMessage>(ev => DirtyFilter(ev.Entity));
    }

    protected override bool Filter(GoapState state, EntityUid target, ContainingContainer filter)
    {
        BaseContainer? container = null;

        if (filter.OuterContainer && !_container.TryGetOuterContainer(target, Transform(target), out container))
            return false;

        if (!filter.OuterContainer && !_container.TryGetContainingContainer(target, out container))
            return false;

        if (filter.ContainerId != null && container?.ID != filter.ContainerId)
            return false;

        return _whitelist.CheckBoth(container?.Owner, whitelist: filter.Whitelist, blacklist: filter.Blacklist);
    }
}
