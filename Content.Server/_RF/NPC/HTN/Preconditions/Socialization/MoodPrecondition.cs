using Content.Server.NPC;
using Content.Shared._RF.Socialization;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.HTN.Preconditions.Socialization;

/// <summary>
/// Checks the mood level of the entity
/// </summary>
public sealed partial class MoodPrecondition : InvertiblePrecondition
{
    private SocializationSystem _socialization;

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

    /// <summary>
    /// Tags that need to be checked for the mood of the entity
    /// </summary>
    [DataField]
    public List<ProtoId<TagPrototype>> Tags = new();

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _socialization = sysManager.GetEntitySystem<SocializationSystem>();
    }

    public override bool IsMetInvertible(NPCBlackboard blackboard)
    {
        if (!blackboard.TryGetValue<EntityUid>(TargetKey, out var uid, EntityManager))
            return false;

        foreach (var tag in Tags)
        {
            if (!_socialization.HasMoodTag(uid, tag))
                return false;
        }

        var mood = _socialization.GetMood(uid);
        return (MoreThan == null || mood > MoreThan)
               && (LessThan == null || mood < LessThan);
    }
}
