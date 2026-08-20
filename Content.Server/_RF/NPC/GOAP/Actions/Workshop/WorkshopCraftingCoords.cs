using Content.Server._RF.NPC.GOAP.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.Workshops.Components;
using Content.Shared._RF.Workshops.Systems;
using Robust.Shared.Map;

namespace Content.Server._RF.NPC.GOAP.Actions.Workshop;

/// <summary>
/// Saves to the state the coordinates of the target workshop's crafting location.
/// </summary>
public sealed partial class WorkshopCraftingCoords : BaseGoapAction<WorkshopCraftingCoords>
{
    /// <summary>
    /// Target workshop entity.
    /// </summary>
    [DataField(required: true)]
    public StateKey<EntityUid> TargetKey;

    /// <summary>
    /// The key in which the coordinates will be stored.
    /// </summary>
    [DataField]
    public StateKey<EntityCoordinates> ResultKey = "TargetCoordinates";
}

public sealed class WorkshopCraftingCoordsGoapActionSystem : GoapActionSystem<WorkshopCraftingCoords>
{
    [Dependency] private readonly WorkshopSystem _workshop = default!;

    protected override bool ActionStartup(Entity<GoapComponent> ent, WorkshopCraftingCoords action)
    {
        if (!TryGetValue(ent, action, action.TargetKey, out var target))
            return false;

        if (!TryComp(target, out WorkshopComponent? comp))
        {
            ComponentNotFound<WorkshopComponent>(ent, action, target);
            return false;
        }

        Set(ent, action, action.ResultKey, _workshop.GetCraftingPlace(new(target, comp)));
        return true;
    }
}
