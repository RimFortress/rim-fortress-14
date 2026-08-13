using Content.Server._RF.NPC.GOAP.Systems;
using Content.Server.Hands.Systems;
using Content.Server.Interaction;
using Content.Server.Storage.EntitySystems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Storage;
using Content.Shared.Tools.Components;
using JetBrains.Annotations;
using Robust.Server.Containers;

namespace Content.Server._RF.NPC.GOAP.Actions.Interaction;

/// <summary>
/// The agent attempts to take the target entity into its active hand.
/// </summary>
public sealed partial class Pickup : BaseGoapAction<Pickup>
{
    /// <summary>
    /// Target entity key.
    /// </summary>
    [DataField]
    public StateKey<EntityUid> TargetKey = "Target";
}

public sealed class PickupActionSystem : GoapActionSystem<Pickup>
{
    [Dependency] private readonly HandsSystem _hands = default!;
    [Dependency] private readonly StorageSystem _storage = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly ContainerSystem _container = default!;
    [Dependency] private readonly InteractionSystem _interaction = default!;

    protected override bool ActionStartup(Entity<GoapComponent> ent, Pickup action)
        => TryGetValue(ent, action, action.TargetKey, out var target)
           && Pickup(ent, target, action);

    /// <summary>
    /// The NPC will attempt to pick up the target entity, freeing up its active hand if necessary.
    /// </summary>
    /// <param name="ent">NPC entity.</param>
    /// <param name="target">Target entity.</param>
    /// <param name="action">GOAP action.</param>
    /// <returns>True, if the entity was successfully picked up</returns>
    [PublicAPI]
    public bool Pickup(Entity<GoapComponent> ent, EntityUid target, GoapAction action)
    {
        if (!HasComp<ItemComponent>(target))
        {
            ComponentNotFound<ItemComponent>(ent, action, target);
            return false;
        }

        if (_container.TryGetContainingContainer(target, out var container))
        {
            if (container.Owner != ent.Owner && _hands.TryGetHand(container.Owner, container.ID, out _))
            {
                CreateDump(ent,
                    action,
                    $"{ToPrettyString(target)} currently in hands of {ToPrettyString(container.Owner)}");
                return false;
            }
        }

        var coords = Transform(target).Coordinates;
        var ownerCoords = Goap.GetValue(ent.Comp.State, GoapState.OwnerCoordinates);
        var interactRange = Goap.GetValue(ent.Comp.State, GoapState.InteractRange);

        if (!coords.TryDistance(EntityManager, ownerCoords, out var dist) || dist > interactRange)
        {
            CreateDump(ent, action, $"{ToPrettyString(target)} not in interact range: {interactRange}");
            return false;
        }

        // If we have an item in hands, we put it away in inventory
        if (TryGetValue(ent, action, GoapState.ActiveHandEntity, out var handItem) && handItem != target)
        {
            // If the welder is turned on in hands, turn it off first
            if (TryComp(handItem, out WelderComponent? welder)
                && TryComp(handItem, out TransformComponent? itemForm)
                && welder.Enabled)
            {
                CreateDump(ent, action, "turning off welder");
                _interaction.UserInteraction(ent, itemForm.Coordinates, handItem);
            }

            if (!_hands.TrySelectEmptyHand(ent))
            {
                var stored = false;

                foreach (var entity in _inventory.GetHandOrInventoryEntities(ent.Owner))
                {
                    if (!TryComp(entity, out StorageComponent? storage)
                        || !_storage.Insert(entity, handItem, out _, storageComp: storage))
                        continue;

                    CreateDump(ent, action, $"{ToPrettyString(handItem)} stored in {ToPrettyString(entity)}");
                    stored = true;
                    break;
                }

                // If we couldn't put the item in the inventory, we throw it away
                if (!stored)
                {
                    if (!_hands.TryDrop(ent.Owner))
                    {
                        CreateDump(ent, action, $"failed to drop {ToPrettyString(handItem)} from the hands");
                        return false;
                    }

                    CreateDump(ent, action, $"{ToPrettyString(handItem)} was thrown from the hands");
                }
            }
        }

        // Pick up the item
        if (handItem != target && !_hands.TryPickup(ent, target))
        {
            CreateDump(ent, action, $"failed to pick up {ToPrettyString(target)}");
            return false;
        }

        return true;
    }
}
