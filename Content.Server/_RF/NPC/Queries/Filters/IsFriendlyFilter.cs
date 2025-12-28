using Content.Server.NPC;
using Content.Shared.NPC.Systems;

namespace Content.Server._RF.NPC.Queries.Filters;

/// <summary>
/// Filters entities that are friendly to the given
/// </summary>
public sealed partial class IsFriendlyFilter : RfUtilityQueryFilter
{
    private NpcFactionSystem _faction;

    public override void Initialize(IEntityManager entManager)
    {
        base.Initialize(entManager);
        _faction = entManager.System<NpcFactionSystem>();
    }

    public override bool Filter(EntityUid uid, NPCBlackboard blackboard)
        => _faction.IsEntityFriendly(blackboard.GetValue<EntityUid>(NPCBlackboard.Owner), uid);
}
