using Content.Shared._RF.NPC;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Systems;

namespace Content.Server._RF.NPC.Search.Filters;

public sealed partial class Captured : BaseSearchFilter<Captured>;

public sealed class CapturedSearchFilterSystem : NpcSearchFilterSystem<Captured>
{
    [Dependency] private readonly OwnershipSystem _ownership = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeTrackedDirty<SearchResultCaptured>();
        SubscribeTrackedDirty<SearchResultReleased>();
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
