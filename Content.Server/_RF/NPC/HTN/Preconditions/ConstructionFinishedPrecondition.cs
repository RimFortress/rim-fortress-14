using Content.Server.Construction.Components;
using Content.Server.NPC;

namespace Content.Server._RF.NPC.HTN.Preconditions;

/// <summary>
/// Checks the entity for components
/// </summary>
public sealed partial class ConstructionFinishedPrecondition : InvertiblePrecondition
{
    [DataField(required: true)]
    public string Key = default!;

    public override bool IsMetInvertible(NPCBlackboard blackboard)
    {
        return blackboard.TryGetValue(Key, out EntityUid? target, EntityManager)
               && EntityManager.TryGetComponent(target, out ConstructionComponent? component)
               && string.IsNullOrEmpty(component.TargetNode);
    }
}
