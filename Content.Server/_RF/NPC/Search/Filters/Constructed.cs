using Content.Server.Construction;
using Content.Server.Construction.Components;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Components;
using Content.Shared._RF.NPC.Search.Systems;

namespace Content.Server._RF.NPC.Search.Filters;

/// <summary>
/// Filters structures whose construction has been completed.
/// </summary>
public sealed partial class Constructed : BaseSearchFilter<Constructed>;

public sealed partial class ConstructedFilterSystem : NpcSearchFilterSystem<Constructed>
{
    [Dependency] private EntityQuery<ConstructionComponent> _query;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SearchTrackedComponent, ConstructionPathfindingUpdated>((ent, ref _) => DirtyFilter(ent.Owner));
    }

    protected override bool Filter(GoapState state, EntityUid target, Constructed filter)
        => _query.TryComp(target, out var comp) && string.IsNullOrEmpty(comp.TargetNode);
}
