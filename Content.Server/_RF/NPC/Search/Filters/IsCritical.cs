using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;

namespace Content.Server._RF.NPC.Search.Filters;

/// <summary>
/// Filters critical entities.
/// </summary>
public sealed partial class IsCritical : BaseSearchFilter<IsCritical>;

public sealed partial class IsCriticalGoapActionSystem : NpcSearchFilterSystem<IsCritical>
{
    [Dependency] private MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MobStateChangedEvent>(ev => DirtyFilter(ev.Target));
    }

    protected override bool Filter(GoapState state, EntityUid target, IsCritical filter)
        => _mobState.IsCritical(target);
}
