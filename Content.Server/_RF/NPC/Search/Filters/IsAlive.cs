using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Components;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;

namespace Content.Server._RF.NPC.Search.Filters;

/// <summary>
/// Filters alive entities.
/// </summary>
public sealed partial class IsAlive : BaseSearchFilter<IsAlive>;

public sealed class IsAliveSystem : NpcSearchFilterSystem<IsAlive>
{
    [Dependency] private readonly MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SearchTrackedComponent, MobStateChangedEvent>((ent, ref _) => DirtyFilter(ent.Owner));
    }

    protected override bool Filter(GoapState state, EntityUid target, IsAlive filter)
        => _mobState.IsAlive(target);
}
