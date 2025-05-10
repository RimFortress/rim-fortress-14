using Content.Server.Botany.Components;
using Content.Server.NPC;

namespace Content.Server._RF.NPC.HTN.Preconditions.Farming;

public sealed partial class NeedSharpForHarvestPrecondition : InvertiblePrecondition
{
    [DataField(required: true)]
    public string TargetKey;

    public override bool IsMetInvertible(NPCBlackboard blackboard)
    {
        return blackboard.TryGetValue(TargetKey, out EntityUid? uid, EntityManager)
               && EntityManager.TryGetComponent(uid, out PlantHolderComponent? comp)
               && comp.Seed is { Ligneous: true };
    }
}
