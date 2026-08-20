using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Components;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;

namespace Content.Server._RF.NPC.Search.Filters;

/// <summary>
/// Filters closed entities with <see cref="OpenableComponent"/>.
/// </summary>
public sealed partial class IsClosed : BaseSearchFilter<IsClosed>;

public sealed class IsClosedSystem : NpcSearchFilterSystem<IsClosed>
{
    [Dependency] private readonly OpenableSystem _openable = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SearchTrackedComponent, OpenableOpenedEvent>((ent, ref _) => DirtyFilter(ent.Owner));
        SubscribeLocalEvent<SearchTrackedComponent, OpenableClosedEvent>((ent, ref _) => DirtyFilter(ent.Owner));
    }

    protected override bool Filter(GoapState state, EntityUid target, IsClosed filter)
        => _openable.IsClosed(target);
}
