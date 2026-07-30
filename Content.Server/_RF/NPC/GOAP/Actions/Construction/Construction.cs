using Content.Server._RF.NPC.GOAP.Actions.Interaction;
using Content.Server._RF.NPC.GOAP.Actions.Movement;
using Content.Server._RF.NPC.GOAP.Systems;
using Content.Server.Interaction;
using Content.Server.NPC.Pathfinding;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared.Tools.Components;

namespace Content.Server._RF.NPC.GOAP.Actions.Construction;

/// <summary>
/// A complex operator responsible for the entire construction logic.
/// It takes the construction target and a list of all ingredients as input;
/// the AI will automatically gather all these items and use them in the construction.
/// </summary>
public sealed partial class Construction : BaseGoapAction<Construction>
{
    /// <summary>
    /// Key that contains the target entity.
    /// </summary>
    [DataField(required: true)]
    public StateKey<EntityUid> TargetKey;

    /// <summary>
    /// The key from which the list of construction materials will be taken.
    /// </summary>
    [DataField]
    public StateKey<List<EntityUid>> ItemsKey = "ConstructionItems";

    /// <summary>
    /// Where the pathfinding result will be stored (if applicable). This gets removed after execution.
    /// </summary>
    [DataField]
    public StateKey<PathResultEvent> PathfindKey = "MovementPathfinding";

    /// <summary>
    /// How close we need to get before considering movement finished.
    /// </summary>
    [DataField]
    public StateKey<float> RangeKey = GoapState.InteractRange;

    /// <summary>
    /// The key where the ID of the current `doAfter` is stored.
    /// </summary>
    public readonly StateKey<ushort> CurrentDoAfter = "CurrentConstructionInteractDoAfter";
}

public sealed class NpcConstructionSystem : GoapActionSystem<Construction>
{
    [Dependency] private readonly InteractionSystem _interaction = default!;
    [Dependency] private readonly InteractWithSystem _interactWith = default!;
    [Dependency] private readonly MoveToSystem _moveTo = default!;
    [Dependency] private readonly PickupActionSystem _pickup = default!;

    protected override float ActionCost(Entity<GoapComponent> ent, GoapState state, Construction action) => 3f;

    protected override bool ActionStartup(Entity<GoapComponent> ent, Construction action)
    {
        ent.Comp.State.Remove(action.CurrentDoAfter);
        return true;
    }

    protected override void ActionShutdown(Entity<GoapComponent> ent, Construction action)
    {
        ent.Comp.State.Remove(action.CurrentDoAfter);
        ent.Comp.State.Remove(action.ItemsKey);
        _moveTo.ShutdownMovement(ent, action.PathfindKey);
    }

    protected override GoapActionResult ActionUpdate(Entity<GoapComponent> ent, Construction action)
    {
        if (!TryGetValue(ent, action, action.TargetKey, out var target)
            || !TryGetValue(ent, action, action.ItemsKey, out var items))
            return GoapActionResult.Failed;

        if (items.Count == 0)
            return GoapActionResult.Finished;

        var item = items[0];

        // If we already started an interaction for this item, check its outcome FIRST,
        // before anything else - including before the Deleted() check below. A successful
        // interaction very often consumes/deletes `item` as part of finishing (e.g. the
        // material gets built into the structure), so `item` legitimately not existing
        // anymore is an expected side effect of success here, not a failure. If we checked
        // Deleted(item) first, a doAfter that finished (and deleted the item) between two
        // updates would be misread as the item having gone missing out from under us.
        if (TryGetValue(ent, action, action.CurrentDoAfter, out _))
        {
            var pendingResult = _interactWith.DoInteraction(ent, action, target, action.CurrentDoAfter, false);

            if (pendingResult != GoapActionResult.Finished)
                return pendingResult;

            return AdvanceToNextItem(ent, action, items);
        }

        // No interaction in flight for this item yet, so it genuinely should still exist -
        // we're about to walk to it / pick it up.
        if (Deleted(item))
        {
            CreateDump(ent, action, $"{ToPrettyString(item)} not exist");
            return GoapActionResult.Failed;
        }

        var itemCoords = Transform(item).Coordinates;
        var ownerCoords = Goap.GetValue(ent.Comp.State, GoapState.OwnerCoordinates);
        var targetCoords = Transform(target).Coordinates;
        float distance;

        if (!TryGetValue(ent, action, GoapState.ActiveHandEntity, out var heldEnt) || heldEnt != item)
        {
            if (!TryGetValue(ent, action, action.RangeKey, out var range)
                || !ownerCoords.TryDistance(EntityManager, itemCoords, out distance))
                return GoapActionResult.Failed;

            // Movement
            if (distance > range)
            {
                if (!_moveTo.StartedUp(ent))
                {
                    CreateDump(ent, action, $"started moving toward the item: {ToPrettyString(item)}");
                    _moveTo.StartupMovement(ent, action, itemCoords, true, action.PathfindKey, action.RangeKey, false);
                }

                var result = _moveTo.UpdateMovement(ent, action, itemCoords, action.PathfindKey, action.RangeKey, false);

                if (result != GoapActionResult.Finished)
                    return result;
            }
            else if (_moveTo.StartedUp(ent))
                _moveTo.ShutdownMovement(ent, action.PathfindKey);

            // Pick up the item
            if (!_pickup.Pickup(ent, item, action))
                return GoapActionResult.Failed;

            CreateDump(ent, action, $"{ToPrettyString(item)} picked up");

            // Turn on welder
            if (TryComp(item, out WelderComponent? welder) && !welder.Enabled)
            {
                CreateDump(ent, action, "turning on welder");
                _interaction.UserInteraction(ent, Transform(item).Coordinates, item);
            }
        }

        if (ownerCoords.TryDistance(EntityManager, targetCoords, out distance)
            && distance > Goap.GetValue(ent.Comp.State, GoapState.InteractRange))
        {
            if (!_moveTo.StartedUp(ent))
            {
                CreateDump(ent, action, $"started moving toward the target: {ToPrettyString(target)}");
                _moveTo.StartupMovement(ent, action, targetCoords, true, action.PathfindKey, action.RangeKey, false);
            }

            var result = _moveTo.UpdateMovement(ent, action, targetCoords, action.PathfindKey, action.RangeKey, false);

            if (result != GoapActionResult.Finished)
                return result;
        }
        else if (_moveTo.StartedUp(ent))
            _moveTo.ShutdownMovement(ent, action.PathfindKey);

        var interactResult = _interactWith.DoInteraction(ent, action, target, action.CurrentDoAfter, false);

        if (interactResult != GoapActionResult.Finished)
            return interactResult;

        return AdvanceToNextItem(ent, action, items);
    }

    /// <summary>
    /// Called once the interaction for <c>items[0]</c> has finished successfully. Clears the
    /// per-item doAfter tracking so the next item starts a fresh interaction (instead of
    /// re-reading this item's already-finished doAfter status), and either advances the item
    /// list or finishes the whole action if that was the last item.
    /// </summary>
    private static GoapActionResult AdvanceToNextItem(Entity<GoapComponent> ent, Construction action, List<EntityUid> items)
    {
        ent.Comp.State.Remove(action.CurrentDoAfter);

        if (items.Count == 1)
            return GoapActionResult.Finished;

        items.RemoveAt(0);
        ent.Comp.State.SetValue(action.ItemsKey, items);
        return GoapActionResult.Continuing;
    }
}
