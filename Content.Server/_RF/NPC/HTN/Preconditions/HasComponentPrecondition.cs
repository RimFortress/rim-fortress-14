using Content.Server.NPC;
using Content.Server.NPC.HTN.Preconditions;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.HTN.Preconditions;

/// <summary>
/// Checks the entity for components
/// </summary>
public sealed partial class HasComponentPrecondition : HTNPrecondition
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    [DataField]
    public string TargetKey = NPCBlackboard.Owner;

    [DataField(required: true)]
    public ComponentRegistry Components = new();

    [DataField]
    public bool Invert;

    public override bool IsMet(NPCBlackboard blackboard)
    {
        if (!blackboard.TryGetValue<EntityUid>(TargetKey, out var owner, _entManager))
            return Invert;

        foreach (var comp in Components)
        {
            var hasComp = _entManager.HasComponent(owner, comp.Value.Component.GetType());
            if (!hasComp && !Invert || Invert && hasComp)
                return false;
        }

        return true;
    }
}
