using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server._RF.NPC.Systems;
using Content.Server.Kitchen.Components;
using Content.Server.Kitchen.EntitySystems;
using Content.Shared._RF.NPC;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.Components;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Kitchen;
using Content.Shared.Stacks;
using JetBrains.Annotations;
using Robust.Server.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.Kitchen;

/// <summary>
/// Helper system for NPC cooking task
/// </summary>
public sealed class NpcKitchenSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ContainerSystem _container = default!;
    [Dependency] private readonly OwnershipSystem _ownership = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly NpcHelperSystem _npcHelper = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;

    private readonly Dictionary<EntProtoId, List<FoodRecipePrototype>> _recipes = new();

    /// <inheritdoc/>
    public override void Initialize()
    {
        _proto.PrototypesReloaded += args =>
        {
            if (args.WasModified<FoodRecipePrototype>())
                ReloadPrototypes();
        };

        ReloadPrototypes();
    }

    private void ReloadPrototypes()
    {
        _recipes.Clear();

        foreach (var proto in _proto.EnumeratePrototypes<FoodRecipePrototype>())
        {
            if (!_recipes.ContainsKey(proto.Result))
                _recipes[proto.Result] = new();

            _recipes[proto.Result].Add(proto);
        }

        foreach (var (protoId, list) in _recipes)
        {
            _recipes[protoId] = list.OrderBy(x => x.IngredientCount()).ToList();
        }
    }

    private void GetAllCollectedItems(
        EntityUid user,
        Entity<MicrowaveComponent?> kitchen,
        out Dictionary<EntProtoId, FixedPoint2> items)
    {
        items = new();

        var inventory = _npcHelper.InventoryEntities(user);

        foreach (var uid in inventory)
        {
            if (Prototype(uid) is { } proto && !items.TryAdd(proto, 1))
                 items[proto] += 1;
        }

        if (!Resolve(kitchen, ref kitchen.Comp))
            return;

        foreach (var uid in kitchen.Comp.Storage.ContainedEntities)
        {
            if (Prototype(uid) is { } proto && !items.TryAdd(proto, 1))
                items[proto] += 1;
        }
    }

    private void GetAllCollectedReagent(
        EntityUid user,
        Entity<MicrowaveComponent?> kitchen,
        out Dictionary<ProtoId<ReagentPrototype>, FixedPoint2> reagents)
    {
        reagents = new();

        var inventory = _npcHelper.InventoryEntities(user);

        foreach (var uid in inventory)
        {
            if (!_solution.TryGetDrainableSolution(uid, out _, out var sol))
                continue;

            foreach (var quantity in sol)
            {
                if (!reagents.TryAdd(quantity.Reagent.Prototype, quantity.Quantity))
                    reagents[quantity.Reagent.Prototype] += quantity.Quantity;
            }
        }

        if (!Resolve(kitchen, ref kitchen.Comp))
            return;

        foreach (var uid in kitchen.Comp.Storage.ContainedEntities)
        {
            if (!_solution.TryGetDrainableSolution(uid, out _, out var sol))
                continue;

            foreach (var quantity in sol)
            {
                if (!reagents.TryAdd(quantity.Reagent.Prototype, quantity.Quantity))
                    reagents[quantity.Reagent.Prototype] += quantity.Quantity;
            }
        }
    }

    [PublicAPI]
    public bool TryGetNextCookingIngredient(
        EntityUid user,
        Entity<MicrowaveComponent?> kitchen,
        ProtoId<FoodRecipePrototype> protoId,
        [NotNullWhen(true)] out EntityUid? ingredient)
    {
        ingredient = null;
        (EntityUid Uid, float Dist)? nearest = null;
        EntProtoId? next = null;

        if (!_proto.Resolve(protoId, out var recipe))
            return false;

        GetAllCollectedItems(user, kitchen, out var query);

        // Looking for the first ingredient that hasn't been collected yet
        foreach (var (id, count) in recipe.IngredientsSolids)
        {
            if (!query.TryGetValue(id, out var quantity) || count > quantity)
            {
                next = id;
                break;
            }

            query[id] -= count;
        }

        if (next == null)
            return false;

        var coords = Transform(user).Coordinates;
        var mapId = Transform(user).MapID;
        var enumerator = _ownership.GetEntitiesEnumerator<TransformComponent, ItemComponent>(user);

        // Search for the entity closest to the user that can be used as an ingredient
        while (enumerator.MoveNext(out var uid, out var xform, out _))
        {
            if (Prototype(uid) is not { } proto || proto != next)
                continue;

            if (xform.MapID != mapId
                || !coords.TryDistance(EntityManager, xform.Coordinates, out var distance)
                || nearest?.Dist < distance)
                continue;

            if(_inventory.TryGetContainingSlot(uid, out _))
                continue;

            if (_container.TryGetContainingContainer(new(uid, xform, null), out var container)
                && HasComp<HandsComponent>(container.Owner))
                continue;

            nearest = (uid, distance);
        }

        if (nearest == null)
            return false;

        ingredient = nearest.Value.Uid;
        return true;
    }

    [PublicAPI]
    public bool TryGetNextCookingReagent(
        EntityUid user,
        Entity<MicrowaveComponent?> kitchen,
        ProtoId<FoodRecipePrototype> protoId,
        [NotNullWhen(true)] out EntityUid? entity,
        [NotNullWhen(true)] out ReagentQuantity? reagent)
    {
        entity = null;
        reagent = null;

        if (!_proto.Resolve(protoId, out var recipe))
            return false;

        (EntityUid Uid, float Dist, FixedPoint2 Quan)? nearest = null;
        (ProtoId<ReagentPrototype> Id, FixedPoint2 Quantity)? next = null;

        GetAllCollectedReagent(user, kitchen, out var query);

        // Looking for the first reagent that hasn't been collected yet
        foreach (var (id, count) in recipe.IngredientsReagents)
        {
            if (!query.TryGetValue(id, out var quantity) || count > quantity)
            {
                next = (id, count);
                break;
            }

            query[id] -= count;
        }

        if (next == null)
            return false;

        var coords = Transform(user).Coordinates;
        var mapId = Transform(user).MapID;
        var enumerator = _ownership.GetEntitiesEnumerator<TransformComponent, SolutionContainerManagerComponent>(user);

        // Search for the entity closest to the user that can be used as an ingredient
        while (enumerator.MoveNext(out var uid, out var xform, out _))
        {
            var quantity = _solution.GetTotalPrototypeQuantity(uid, next.Value.Id);

            if (quantity == 0)
                continue;

            if (quantity > next.Value.Quantity)
                quantity = next.Value.Quantity;

            if (nearest?.Quan > quantity && quantity < next.Value.Quantity)
                continue;

            if (xform.MapID != mapId
                || !coords.TryDistance(EntityManager, xform.Coordinates, out var distance)
                || nearest?.Dist < distance)
                continue;

            if(_inventory.TryGetContainingSlot(uid, out _))
                continue;

            if (_container.TryGetContainingContainer(new(uid, xform, null), out var container)
                && HasComp<HandsComponent>(container.Owner))
                continue;

            nearest = (uid, distance, quantity);
        }

        if (nearest == null)
            return false;

        entity = nearest.Value.Uid;
        reagent = new ReagentQuantity(next.Value.Id, nearest.Value.Quan);
        return true;
    }

    /// <summary>
    /// Builds a path from recipes to create the target recipe.
    /// </summary>
    /// <param name="user">User entity.</param>
    /// <param name="target">Target recipe.</param>
    /// <param name="path">A recipe path whose last element is the target.</param>
    /// <returns>True, if the path is found.</returns>
    public bool TryGetRecipesPath(
        EntityUid user,
        ProtoId<FoodRecipePrototype> target,
        [NotNullWhen(true)] out List<ProtoId<FoodRecipePrototype>>? path)
    {
        path = null;

        var available = new Dictionary<EntProtoId, FixedPoint2>();
        var enumerator = _ownership.GetEntitiesEnumerator<MetaDataComponent, ItemComponent>(user);

        while (enumerator.MoveNext(out var meta, out _))
        {
            if (meta.EntityPrototype is not { } proto)
                continue;

            if (!available.TryAdd(proto, 1))
                available[proto] += 1;
        }

        return TryGetRecipesPath(target, available, out path);
    }

    /// <summary>
    /// Builds a path from recipes to create the target recipe.
    /// </summary>
    /// <param name="target">Target recipe.</param>
    /// <param name="available">A list of all items available for cooking, along with their quantities.</param>
    /// <param name="path">A recipe path whose last element is the target.</param>
    /// <returns>True, if the path is found.</returns>
    private bool TryGetRecipesPath(
        ProtoId<FoodRecipePrototype> target,
        Dictionary<EntProtoId, FixedPoint2> available,
        [NotNullWhen(true)] out List<ProtoId<FoodRecipePrototype>>? path)
    {
        path = new();

        if (!_proto.Resolve(target, out var targetProto))
            return false;

        var query = new Dictionary<EntProtoId, FixedPoint2>(available);
        var recipes = new List<Dictionary<FoodRecipePrototype, FixedPoint2>>();
        var queue = new Queue<(int Depth, FoodRecipePrototype Proto, FixedPoint2 Quantity)>();

        queue.Enqueue((0, targetProto, 1));

        while (queue.TryDequeue(out var recipe))
        {
            if (recipes.Count <= recipe.Depth)
                recipes.Add(new());

            // If several identical ingredients are used at the same depth, we add their quantities together
            if (!recipes[recipe.Depth].TryAdd(recipe.Proto, recipe.Quantity))
                recipes[recipe.Depth][recipe.Proto] += recipe.Quantity;

            foreach (var (protoId, q) in recipe.Proto.IngredientsSolids)
            {
                var quantity = q * recipe.Quantity;

                // If the item is already available in the required quantity, continue
                if (query.TryGetValue(protoId, out var queryQuan))
                {
                    if (queryQuan > quantity)
                    {
                        query[protoId] -= quantity;
                        continue;
                    }

                    if (queryQuan == quantity)
                    {
                        query.Remove(protoId);
                        continue;
                    }

                    quantity -= queryQuan;
                    query.Remove(protoId);
                }

                // Else, search for a recipe for the ingredient
                if (!_recipes.TryGetValue(protoId, out var list))
                    return false;

                if (list.Count == 1)
                {
                    queue.Enqueue((recipe.Depth + 1, list[0], quantity));
                    continue;
                }

                FoodRecipePrototype? valid = null;

                // If multiple recipes are available, we look for the first suitable one
                foreach (var proto in list)
                {
                    if (!ValidRecipe(proto, quantity))
                        continue;

                    valid = proto;
                    break;
                }

                if (valid == null)
                    return false;

                queue.Enqueue((recipe.Depth + 1, valid, quantity));
            }
        }

        foreach (var depth in recipes)
        {
            foreach (var (proto, quan) in depth)
            {
                for (var i = 0; i < quan; i++)
                {
                    path.Add(proto);
                }
            }
        }

        if (path.Count == 0)
            return false;

        path.Reverse();
        return true;

        bool ValidRecipe(FoodRecipePrototype proto, FixedPoint2 recipeQuantity)
        {
            foreach (var (protoId, q) in proto.IngredientsSolids)
            {
                var quantity = q * recipeQuantity;

                if (_recipes.ContainsKey(protoId))
                    continue;

                if (!query.TryGetValue(protoId, out var queryQuan) || quantity > queryQuan)
                    return false;
            }

            return true;
        }
    }

    [PublicAPI]
    public bool CanStartCooking(Entity<MicrowaveComponent?> kitchen, ProtoId<FoodRecipePrototype> protoId)
    {
        if (!Resolve(kitchen, ref kitchen.Comp) || !_proto.Resolve(protoId, out var proto))
            return false;

        var solidsDict = new Dictionary<string, int>();
        var reagentDict = new Dictionary<string, FixedPoint2>();

        foreach (var uid in kitchen.Comp.Storage.ContainedEntities)
        {
            (EntProtoId? Id, int Amount) solid = TryComp<StackComponent>(uid, out var stack)
                ? (_proto.Index<StackPrototype>(stack.StackTypeId).Spawn, stack.Count)
                : (Prototype(uid)?.ID, 1);

            if (solid.Id == null)
                continue;

            if (!solidsDict.TryAdd(solid.Id.Value, solid.Amount))
                solidsDict[solid.Id.Value] += solid.Amount;

            // only use reagents we have access to
            // you have to break the eggs before we can use them!
            if (!_solution.TryGetDrainableSolution(uid, out _, out var solution))
                continue;

            foreach (var (reagent, quantity) in solution.Contents)
            {
                if (!reagentDict.TryAdd(reagent.Prototype, quantity))
                    reagentDict[reagent.Prototype] += quantity;
            }
        }

        return MicrowaveSystem.CanSatisfyRecipe(kitchen.Comp, proto, solidsDict, reagentDict).Item2 > 0;
    }

    /// <summary>
    /// Checks whether the entity is the result of preparing a given recipe.
    /// </summary>
    [PublicAPI]
    public bool IsResult(EntityUid uid, ProtoId<FoodRecipePrototype> protoId)
        => Prototype(uid) is { } entProto
           && _recipes.TryGetValue(entProto, out var recipes)
           && _proto.Resolve(protoId, out var proto)
           && recipes.Contains(proto);
}
