using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Components;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared._RF.NPC.Systems;

namespace Content.Server._RF.NPC.Search.Filters;

public sealed partial class Captured : BaseSearchFilter<Captured>;

public sealed partial class CapturedSearchFilterSystem : NpcSearchFilterSystem<Captured>
{
    [Dependency] private OwnershipSystem _ownership = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SearchTrackedComponent, SearchResultCaptured>((ent, ref _) => DirtyFilter(ent.Owner));
        SubscribeLocalEvent<SearchTrackedComponent, SearchResultReleased>((ent, ref _) => DirtyFilter(ent.Owner));
    }

    protected override bool Filter(GoapState state, EntityUid target, Captured filter)
    {
        if (!TrackedQuery.TryComp(target, out var comp)
            || comp.Captured.Count == 0)
            return false;

        var owner = SharedGoapSystem.Owner(state);

        foreach (var uid in comp.Captured)
        {
            if (uid != owner && _ownership.HasSameOwner(uid, owner))
                return true;
        }

        return false;
    }
}
