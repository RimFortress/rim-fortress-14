using Content.Server.Botany.Components;
using Content.Server.NPC;

namespace Content.Server._RF.NPC.HTN.Preconditions.Farming;

public sealed partial class WeedLevelPrecondition : InvertiblePrecondition
{
    [DataField(required: true)]
    public string TargetKey;

    [DataField]
    public float? MoreThan;

    [DataField]
    public float? LessThan;

    public override bool IsMetInvertible(NPCBlackboard blackboard)
    {
        return blackboard.TryGetValue(TargetKey, out EntityUid? uid, EntityManager)
               && EntityManager.TryGetComponent(uid, out PlantHolderComponent? comp)
               && (MoreThan != null && comp.WeedLevel > MoreThan || LessThan != null && comp.WeedLevel < LessThan);
    }
}
