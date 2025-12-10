using Content.Server.NPC;
using Content.Shared._RF.Social.Systems;

namespace Content.Server._RF.NPC.HTN.Preconditions.Social;

/// <summary>
/// Checks the mood level of the entity
/// </summary>
public sealed partial class MoodPrecondition : InvertiblePrecondition
{
    private SocialSystem _social;

    /// <summary>
    /// Key stores entity whose mood needs to be checked
    /// </summary>
    [DataField]
    public string TargetKey = NPCBlackboard.Owner;

    /// <summary>
    /// Minimum mood level
    /// </summary>
    [DataField]
    public int? MoreThan;

    /// <summary>
    /// Maximum mood level
    /// </summary>
    [DataField]
    public int? LessThan;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _social = sysManager.GetEntitySystem<SocialSystem>();
    }

    public override bool IsMetInvertible(NPCBlackboard blackboard)
    {
        if (!blackboard.TryGetValue<EntityUid>(TargetKey, out var uid, EntityManager))
            return false;

        var mood = _social.GetMood(uid);
        return (MoreThan == null || mood > MoreThan)
               && (LessThan == null || mood < LessThan);
    }
}
