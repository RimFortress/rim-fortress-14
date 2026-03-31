using Content.Server._RF.Workshops.Components;
using Content.Server.NPC;

namespace Content.Server._RF.NPC.HTN.Preconditions.Workshop;

/// <summary>
/// Checks whether there are any unfinished recipes in the workshop.
/// </summary>
public sealed partial class WorkshopCraftingDonePrecondition : InvertiblePrecondition
{
    /// <summary>
    /// The key stores the workshop entity.
    /// </summary>
    [DataField]
    public string TargetKey = "Target";

    public override bool IsMetInvertible(NPCBlackboard blackboard)
        => blackboard.TryGetValue<EntityUid>(TargetKey, out var uid, EntityManager)
           && EntityManager.TryGetComponent(uid, out WorkshopComponent? comp)
           && comp.Queue.Count == 0;
}
