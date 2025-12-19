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
    [DataField(required: true)]
    public string ThresholdId;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _needs = sysManager.GetEntitySystem<NeedsSystem>();
    }

    public override bool IsMetInvertible(NPCBlackboard blackboard)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        return _needs.TryGetThreshold(owner, Need, out var threshold) && threshold == ThresholdId;
    }
}
