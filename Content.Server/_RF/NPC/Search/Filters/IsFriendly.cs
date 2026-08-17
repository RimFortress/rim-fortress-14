using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared.NPC.Systems;

namespace Content.Server._RF.NPC.Search.Filters;

/// <summary>
/// Filters entities that are friendly to the agent.
/// </summary>
public sealed partial class IsFriendly : BaseSearchFilter<IsFriendly>;

public sealed class IsFriendlySearchFilterSystem : NpcSearchFilterSystem<IsFriendly>
{
    [Dependency] private readonly NpcFactionSystem _faction = default!;

    protected override bool Filter(GoapState state, EntityUid target, IsFriendly filter)
        => _faction.IsEntityFriendly(Goap.GetValue(state, GoapState.Owner), target);
}
