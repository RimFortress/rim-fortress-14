using Content.Shared._RF.Workshops.Components;
using Content.Server.NPC;

namespace Content.Server._RF.NPC.HTN.Preconditions.Workshop;

/// <summary>
/// Checks whether the workshop is currently in production.
/// </summary>
public sealed partial class WorkshopCraftingPrecondition : InvertiblePrecondition
{
    /// <summary>
    /// The key stores the workshop entity.
    /// </summary>
    [DataField]
    public string TargetKey = "Target";

    public override bool IsMetInvertible(NPCBlackboard blackboard)
        => blackboard.TryGetValue<EntityUid>(TargetKey, out var uid, EntityManager)
           && EntityManager.TryGetComponent(uid, out WorkshopComponent? comp)
           && comp.CraftEndTime != null;
}
