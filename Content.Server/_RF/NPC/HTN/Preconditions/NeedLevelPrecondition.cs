using Content.Server.NPC;
using Content.Shared._RF.Needs;
using Content.Shared._RF.Needs.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.HTN.Preconditions;

/// <summary>
/// Checks the threshold of an entity's need
/// </summary>
public sealed partial class NeedLevelPrecondition : InvertiblePrecondition
{
    private NeedsSystem _needs;

    /// <summary>
    /// Need prototype
    /// </summary>
    [DataField(required: true)]
    public ProtoId<NeedPrototype> Need;

    /// <summary>
    /// ID of the threshold to pass the check
    /// </summary>
    [DataField]
    public string? ThresholdId;

    [DataField]
    public float? MoreThan;

    [DataField]
    public float? LessThan;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _needs = sysManager.GetEntitySystem<NeedsSystem>();
    }

    public override bool IsMetInvertible(NPCBlackboard blackboard)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        var level = _needs.GetValue(owner, Need);

        if (ThresholdId != null && (!_needs.TryGetThreshold(owner, Need, out var threshold) || threshold != ThresholdId))
            return false;

        return (MoreThan == null || level > MoreThan) && (LessThan == null || level < LessThan);
    }
}
