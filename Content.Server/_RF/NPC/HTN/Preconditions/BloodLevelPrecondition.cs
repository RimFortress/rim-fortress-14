using Content.Server.Body.Systems;
using Content.Server.NPC;
using Content.Server.NPC.HTN.Preconditions;

namespace Content.Server._RF.NPC.HTN.Preconditions;

/// <summary>
/// Checks the amount of the entity's blood in percentage terms
/// </summary>
public sealed partial class BloodLevelPrecondition : HTNPrecondition
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    private BloodstreamSystem _bloodstream = default!;

    [DataField]
    public string TargetKey = NPCBlackboard.Owner;

    [DataField]
    public float? MoreThan;

    [DataField]
    public float? LessThan;

    [DataField]
    public bool Invert;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _bloodstream = sysManager.GetEntitySystem<BloodstreamSystem>();
    }

    public override bool IsMet(NPCBlackboard blackboard)
    {
        if (!blackboard.TryGetValue(TargetKey, out EntityUid? uid, _entManager))
            return Invert;

        var bloodLevel = _bloodstream.GetBloodLevelPercentage(uid.Value);

        if (MoreThan != null && bloodLevel > MoreThan)
            return !Invert;

        if (LessThan != null && bloodLevel < LessThan)
            return !Invert;

        return Invert;
    }
}
