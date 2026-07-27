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
        var coords = Transform(item).Coordinates;

        if (!TryGetValue(ent, action, action.RangeKey, out var range)
            || !Transform(ent).Coordinates.TryDistance(EntityManager, coords, out var distance))
            return GoapActionResult.Failed;

        // Movement
        if (distance > range)
        {
            if (!_moveTo.StartedUp(ent))
                _moveTo.StartupMovement(ent, action, coords, true, action.PathfindKey, action.RangeKey, false);

            var result = _moveTo.UpdateMovement(ent, action, coords, action.PathfindKey, action.RangeKey, false);

            if (result != GoapActionResult.Finished)
                return result;
        }
        else if (_moveTo.StartedUp(ent))
            _moveTo.ShutdownMovement(ent, action.PathfindKey);

        // Pick up the item
        if (!_pickup.Pickup(ent, item, action))
            return GoapActionResult.Failed;

        // Turn on welder
        if (TryComp(item, out WelderComponent? welder) && !welder.Enabled)
        {
            CreateDump(ent, action, "turning on welder");
            _interaction.UserInteraction(ent, Transform(item).Coordinates, item);
        }

        var interactResult = _interactWith.DoInteraction(ent, action, target, action.CurrentDoAfter, false);

        if (interactResult != GoapActionResult.Finished)
            return interactResult;

        if (items.Count == 1)
            return GoapActionResult.Finished;

        items.RemoveAt(0);
        ent.Comp.State.SetValue(action.ItemsKey, items);
        return GoapActionResult.Continuing;
    }
}
