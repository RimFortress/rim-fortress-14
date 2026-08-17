using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Systems;

namespace Content.Server._RF.NPC.Search.Filters;

/// <summary>
/// Filters entities to determine whether they are the agent executing the search query.
/// </summary>
public sealed partial class Self : BaseSearchFilter<Self>;

public sealed class SelfSearchFilterSystem : NpcSearchFilterSystem<Self>
{
    protected override bool Filter(GoapState state, EntityUid target, Self filter)
        => target == SharedGoapSystem.Owner(state);
}
