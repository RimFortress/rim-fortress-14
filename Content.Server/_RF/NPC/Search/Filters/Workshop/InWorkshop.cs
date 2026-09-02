using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Components;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared._RF.Workshops;
using Content.Shared._RF.Workshops.Components;
using Robust.Server.Containers;

namespace Content.Server._RF.NPC.Search.Filters.Workshop;

/// <summary>
/// Filters entities located in the ingredient workshop container.
/// </summary>
public sealed partial class InWorkshop : BaseSearchFilter<InWorkshop>;

public sealed partial class InWorkshopSearchFilterSystem : NpcSearchFilterSystem<InWorkshop>
{
    [Dependency] private ContainerSystem _container = default!;
    [Dependency] private readonly EntityQuery<WorkshopComponent> _query = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NpcSearcherComponent, WorkshopQueueAdded>((ent, ref _) =>
            DirtyAgentFilter(ent.AsNullable()));
        SubscribeLocalEvent<NpcSearcherComponent, WorkshopQueueRemoved>((ent, ref _) =>
            DirtyAgentFilter(ent.AsNullable()));
    }

    protected override bool Filter(GoapState state, EntityUid target, InWorkshop filter)
        => _container.TryGetOuterContainer(target, Transform(target), out var container)
           && _query.HasComp(container.Owner);
}
