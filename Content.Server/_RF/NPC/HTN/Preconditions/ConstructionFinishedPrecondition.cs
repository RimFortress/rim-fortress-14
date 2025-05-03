using Content.Server.Construction.Components;
using Content.Server.NPC;

namespace Content.Server._RF.NPC.HTN.Preconditions;

/// <summary>
/// Checks the entity for components
/// </summary>
public sealed partial class ConstructionFinishedPrecondition : InvertiblePrecondition
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    [DataField(required: true)]
    public string Key = default!;

    public override bool IsMetInvertible(NPCBlackboard blackboard)
    {
        return blackboard.TryGetValue(Key, out EntityUid? target, _entManager)
               && _entManager.TryGetComponent(target, out ConstructionComponent? component)
               && string.IsNullOrEmpty(component.TargetNode);
    }
}
