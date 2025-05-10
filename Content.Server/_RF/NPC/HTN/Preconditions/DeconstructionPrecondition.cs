using Content.Server.Construction.Components;
using Content.Server.NPC;

namespace Content.Server._RF.NPC.HTN.Preconditions;

public sealed partial class DeconstructionPrecondition : InvertiblePrecondition
{
    [DataField(required: true)]
    public string Key = default!;

    public override bool IsMetInvertible(NPCBlackboard blackboard)
    {
        return blackboard.TryGetValue(Key, out EntityUid? target, EntityManager)
               && EntityManager.TryGetComponent(target, out ConstructionComponent? component)
               && !string.IsNullOrEmpty(component.DeconstructionNode);
    }
}
