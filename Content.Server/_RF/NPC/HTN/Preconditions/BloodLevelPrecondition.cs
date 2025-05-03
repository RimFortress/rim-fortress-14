using Content.Server.Body.Systems;
using Content.Server.NPC;

namespace Content.Server._RF.NPC.HTN.Preconditions;

/// <summary>
/// Checks the amount of the entity's blood in percentage terms
/// </summary>
public sealed partial class BloodLevelPrecondition : InvertiblePrecondition
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    private BloodstreamSystem _bloodstream = default!;

    [DataField]
    public string TargetKey = NPCBlackboard.Owner;

    [DataField]
    public float? MoreThan;

    [DataField]
    public float? LessThan;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _bloodstream = sysManager.GetEntitySystem<BloodstreamSystem>();
    }

    public override bool IsMetInvertible(NPCBlackboard blackboard)
    {
        if (!blackboard.TryGetValue(TargetKey, out EntityUid? uid, _entManager))
            return false;

        var bloodLevel = _bloodstream.GetBloodLevelPercentage(uid.Value);

        return MoreThan != null && bloodLevel > MoreThan
               || LessThan != null && bloodLevel < LessThan;
    }
}
