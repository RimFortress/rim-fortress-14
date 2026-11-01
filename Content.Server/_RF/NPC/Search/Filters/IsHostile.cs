using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;

namespace Content.Server._RF.NPC.Search.Filters;

/// <summary>
/// Filters entities that are hostile to the agent: a faction of the entity and the factions of the agent are hostile to each other.
/// </summary>
public sealed partial class IsHostile : BaseSearchFilter<IsHostile>;

public sealed partial class IsHostileSearchFilterSystem : NpcSearchFilterSystem<IsHostile>
{
    [Dependency] private NpcFactionSystem _faction = null!;
    [Dependency] private EntityQuery<NpcFactionMemberComponent> _memberQuery;

    protected override bool Filter(GoapState state, EntityUid target, IsHostile filter)
    {
        if (!_memberQuery.TryComp(target, out var member))
            return false;

        var owner = Goap.GetValue(state, GoapState.Owner);

        foreach (var faction in member.Factions)
        {
            if (_faction.IsFactionHostile(faction, owner))
                return true;
        }

        return false;
    }
}
