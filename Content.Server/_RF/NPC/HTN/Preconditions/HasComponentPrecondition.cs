using Content.Server.NPC;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.HTN.Preconditions;

/// <summary>
/// Checks the entity for components
/// </summary>
public sealed partial class HasComponentPrecondition : InvertiblePrecondition
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    [DataField]
    public string TargetKey = NPCBlackboard.Owner;

    [DataField(required: true)]
    public ComponentRegistry Components = new();

    public override bool IsMetInvertible(NPCBlackboard blackboard)
    {
        if (!blackboard.TryGetValue<EntityUid>(TargetKey, out var owner, _entManager))
            return false;

        foreach (var comp in Components)
        {
            if (!_entManager.HasComponent(owner, comp.Value.Component.GetType()))
                return false;
        }

        return true;
    }
}
