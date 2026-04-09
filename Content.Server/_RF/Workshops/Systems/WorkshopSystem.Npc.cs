using System.Diagnostics.CodeAnalysis;
using Content.Shared._RF.Workshops.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.Components;
using Content.Shared.Item;
using Content.Shared.Stacks;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.Workshops.Systems;

public partial class WorkshopSystem
{
    [PublicAPI]
    public bool TryGetNextCraftingItem(
        EntityUid user,
        Entity<WorkshopComponent?> workshop,
        [NotNullWhen(true)] out EntityUid? entity,
        [NotNullWhen(false)] out string? reason)
    {
        reason = string.Empty;
        entity = null;

        if (!Resolve(workshop, ref workshop.Comp) || GetCurrentRecipe(workshop) is not { } protoId)
            return false;

        var ingredients = GetRemainingIngredients(workshop.Comp, protoId);

        foreach (var (material, count) in ingredients.Materials)
        {
            return TryGetMaterial(user, material, count, out entity, out reason);
        }

        foreach (var (ent, _) in ingredients.Items)
        {
            return TryGetIngredient(user, ent, out entity, out reason);
        }

        foreach (var (reagent, quantity) in ingredients.Reagents)
        {
            return TryGetReagent(user, reagent, quantity, out entity, out reason);
        }

        return false;
    }

    private bool TryGetMaterial(
        EntityUid user,
        ProtoId<StackPrototype> stack,
        int count,
        [NotNullWhen(true)] out EntityUid? entity,
        [NotNullWhen(false)] out string? reason)
    {
        reason = string.Empty;
        entity = null;
        (EntityUid Uid, int Count, float Dist)? nearest = null;

        var coords = Transform(user).Coordinates;
        var mapId = Transform(user).MapID;
        var enumerator = Ownership.GetEntitiesEnumerator<StackComponent, TransformComponent>(user);

        // Search for the entity closest to the user that can be used as an ingredient
        while (enumerator.MoveNext(out var uid, out var comp, out var xform))
        {
            if (comp.StackTypeId != stack)
                continue;

            if (nearest?.Count > count && comp.Count < nearest.Value.Count)
                continue;

            if (xform.MapID != mapId
                || !coords.TryDistance(EntityManager, xform.Coordinates, out var distance)
                || nearest?.Dist < distance)
                continue;

            if(_inventory.TryGetContainingSlot(uid, out _))
                continue;

            if (Container.TryGetContainingContainer(new(uid, xform, null), out var container)
                && HasComp<HandsComponent>(container.Owner))
                continue;

            nearest = (uid, comp.Count, distance);
        }

        if (nearest == null)
        {
            reason = _npcHelper.MaterialNotFoundReason(stack, count);
            return false;
        }

        entity = nearest.Value.Uid;
        return true;
    }

    private bool TryGetIngredient(
        EntityUid user,
        EntProtoId protoId,
        [NotNullWhen(true)] out EntityUid? entity,
        [NotNullWhen(false)] out string? reason)
    {
        reason = string.Empty;
        entity = null;
        (EntityUid Uid, float Dist)? nearest = null;

        var coords = Transform(user).Coordinates;
        var mapId = Transform(user).MapID;
        var enumerator = Ownership.GetEntitiesEnumerator<TransformComponent, ItemComponent>(user);

        // Search for the entity closest to the user that can be used as an ingredient
        while (enumerator.MoveNext(out var uid, out var xform, out _))
        {
            if (Prototype(uid) is not { } proto || proto != protoId)
                continue;

            if (xform.MapID != mapId
                || !coords.TryDistance(EntityManager, xform.Coordinates, out var distance)
                || nearest?.Dist < distance)
                continue;

            if(_inventory.TryGetContainingSlot(uid, out _))
                continue;

            if (Container.TryGetContainingContainer(new(uid, xform, null), out var container)
                && HasComp<HandsComponent>(container.Owner))
                continue;

            nearest = (uid, distance);
        }

        if (nearest == null)
        {
            reason = _npcHelper.EntProtoNotFoundReason(protoId);
            return false;
        }

        entity = nearest.Value.Uid;
        return true;
    }

    private bool TryGetReagent(
        EntityUid user,
        ProtoId<ReagentPrototype> reagent,
        FixedPoint2 quantity,
        [NotNullWhen(true)] out EntityUid? entity,
        [NotNullWhen(false)] out string? reason)
    {
        reason = string.Empty;
        entity = null;

        (EntityUid Uid, float Dist, FixedPoint2 Quan)? nearest = null;

        var coords = Transform(user).Coordinates;
        var mapId = Transform(user).MapID;
        var enumerator = Ownership.GetEntitiesEnumerator<TransformComponent, SolutionContainerManagerComponent>(user);

        // Search for the entity closest to the user that can be used as an ingredient
        while (enumerator.MoveNext(out var uid, out var xform, out var sol))
        {
            if (!Solution.TryGetDrainableSolution(new(uid, null, sol), out _, out var solution))
                continue;

            var quan = solution.GetTotalPrototypeQuantity(reagent);

            if (quan == 0)
                continue;

            if (quan > quantity)
                quan = quantity;

            if (nearest?.Quan > quan && quan < quantity)
                continue;

            if (xform.MapID != mapId
                || !coords.TryDistance(EntityManager, xform.Coordinates, out var distance)
                || nearest?.Dist < distance)
                continue;

            if(_inventory.TryGetContainingSlot(uid, out _))
                continue;

            if (Container.TryGetContainingContainer(new(uid, xform, null), out var container)
                && HasComp<HandsComponent>(container.Owner))
                continue;

            nearest = (uid, distance, quantity);
        }

        if (nearest == null)
        {
            reason = _npcHelper.ReagentNotFoundReason(reagent, quantity);
            return false;
        }

        entity = nearest.Value.Uid;
        return true;
    }
}
