using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared.Movement.Pulling.Components;

namespace Content.Server._RF.NPC.Search.Filters;

/// <summary>
/// Filters entities that are pulled by another.
/// </summary>
public sealed partial class Pulled : BaseSearchFilter<Pulled>;

public sealed class PulledSearchFilterSystem : NpcSearchFilterSystem<Pulled>
{
    [Dependency] private readonly EntityQuery<PullableComponent> _query = default!;

    protected override bool Filter(GoapState state, EntityUid target, Pulled filter)
        => _query.TryComp(target, out var comp) && comp.Puller != null;
}
