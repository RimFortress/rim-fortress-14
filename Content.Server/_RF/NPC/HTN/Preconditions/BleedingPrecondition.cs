using Content.Server.Body.Components;
using Content.Server.NPC;
using Content.Server.NPC.HTN.Preconditions;

namespace Content.Server._RF.NPC.HTN.Preconditions;

/// <summary>
/// Check if the entity is bleeding.
/// </summary>
public sealed partial class BleedingPrecondition : HTNPrecondition
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    [DataField(required: true)]
    public string TargetKey = default!;

    [DataField]
    public bool Invert;

    public override bool IsMet(NPCBlackboard blackboard)
    {
        if (blackboard.TryGetValue(TargetKey, out EntityUid? uid, _entManager)
            && _entManager.TryGetComponent(uid, out BloodstreamComponent? bloodstream)
            && bloodstream.BleedReductionAmount > 0)
            return !Invert;

        return Invert;
    }
}
