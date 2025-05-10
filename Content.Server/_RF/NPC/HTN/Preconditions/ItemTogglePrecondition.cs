using Content.Server.NPC;
using Content.Shared.Item.ItemToggle.Components;

namespace Content.Server._RF.NPC.HTN.Preconditions;

public sealed partial class ItemTogglePrecondition : InvertiblePrecondition
{
    [DataField(required: true)]
    public string TargetKey = default!;

    public override bool IsMetInvertible(NPCBlackboard blackboard)
    {
        return blackboard.TryGetValue(TargetKey, out EntityUid? uid, EntityManager)
               && EntityManager.TryGetComponent(uid, out ItemToggleComponent? comp)
               && comp.Activated;
    }
}
