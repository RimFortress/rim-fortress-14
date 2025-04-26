using Content.Server.Construction.Components;
using Content.Server.NPC;
using Content.Server.NPC.HTN.Preconditions;

namespace Content.Server._RF.NPC.HTN.Preconditions;

/// <summary>
/// Checks the entity for components
/// </summary>
public sealed partial class ConstructionFinishedPrecondition : HTNPrecondition
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    [DataField(required: true)]
    public string Key = default!;

    [DataField]
    public bool Invert;

    public override bool IsMet(NPCBlackboard blackboard)
    {
        if (blackboard.TryGetValue(Key, out EntityUid? target, _entManager)
            && _entManager.TryGetComponent(target, out ConstructionComponent? component)
            && string.IsNullOrEmpty(component.TargetNode))
            return !Invert;

        return Invert;
    }
}
