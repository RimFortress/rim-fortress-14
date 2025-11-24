using Content.Server.NPC;
using Content.Shared._RF.Socialization;

namespace Content.Server._RF.NPC.HTN.Preconditions.Socialization;

/// <summary>
/// Checks the level of the entity's opinion of another
/// </summary>
public sealed partial class OpinionPrecondition : InvertiblePrecondition
{
    private SocializationSystem _socialization;

    /// <summary>
    /// Key with target entity
    /// </summary>
    [DataField]
    public string TargetKey = "TargetKey";

    /// <summary>
    /// Minimum level of opinion
    /// </summary>
    [DataField]
    public int? MoreThan;

    /// <summary>
    /// Maximum level of opinion
    /// </summary>
    [DataField]
    public int? LessThan;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _socialization = sysManager.GetEntitySystem<SocializationSystem>();
    }

    public override bool IsMetInvertible(NPCBlackboard blackboard)
    {
        if (!blackboard.TryGetValue<EntityUid>(TargetKey, out var uid, EntityManager))
            return false;

        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        var opinion = _socialization.GetOpinion(owner, uid);

        return (MoreThan == null || opinion > MoreThan)
            && (LessThan == null || opinion < LessThan);
    }
}
