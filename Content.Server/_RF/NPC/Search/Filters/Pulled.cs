using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Components;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Events;

namespace Content.Server._RF.NPC.Search.Filters;

/// <summary>
/// Filters entities that are pulled by another.
/// </summary>
public sealed partial class Pulled : BaseSearchFilter<Pulled>;

public sealed partial class PulledSearchFilterSystem : NpcSearchFilterSystem<Pulled>
{
    [Dependency] private EntityQuery<PullableComponent> _query;

    [SubscribeLocalEvent]
    private void OnPullStarted(Entity<SearchTrackedComponent> ent, ref PullStartedMessage ev)
    {
        if (ev.PulledUid == ent.Owner)
            DirtyFilter(ent.AsNullable());
    }

    [SubscribeLocalEvent]
    private void OnPullStopped(Entity<SearchTrackedComponent> ent, ref PullStoppedMessage ev)
    {
        if (ev.PulledUid == ent.Owner)
            DirtyFilter(ent.AsNullable());
    }

    protected override bool Filter(GoapState state, EntityUid target, Pulled filter)
        => _query.TryComp(target, out var comp) && comp.Puller != null;
}
