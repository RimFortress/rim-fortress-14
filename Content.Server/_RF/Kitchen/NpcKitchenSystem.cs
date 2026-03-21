using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server._RF.Stockpile;
using Content.Shared._RF.NPC;
using Content.Shared.FixedPoint;
using Content.Shared.Kitchen;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.Kitchen;

/// <summary>
/// Helper system for NPC cooking task
/// </summary>
public sealed class NpcKitchenSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly StockpileSystem _stockpile = default!;
    [Dependency] private readonly OwnedSystem _owned = default!;

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

    /// <summary>
    /// Builds a path from recipes to create the target recipe.
    /// </summary>
    /// <param name="user">User entity.</param>
    /// <param name="target">Target recipe.</param>
    /// <param name="path">A recipe path whose last element is the target.</param>
    /// <returns>True, if the path is found.</returns>
    private bool TryGetRecipesPath(
        EntityUid user,
        FoodRecipePrototype target,
        [NotNullWhen(true)] out List<(FoodRecipePrototype Proto, FixedPoint2 Quantity)>? path)
    {
        path = null;

        foreach (var uid in _owned.GetOwners(user))
        {
            if (TryGetRecipesPath(target, _stockpile.GetAllPrototypes(uid), out path))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Builds a path from recipes to create the target recipe.
    /// </summary>
    /// <param name="target">Target recipe.</param>
    /// <param name="available">A list of all items available for cooking, along with their quantities.</param>
    /// <param name="path">A recipe path whose last element is the target.</param>
    /// <returns>True, if the path is found.</returns>
    private bool TryGetRecipesPath(
        FoodRecipePrototype target,
        Dictionary<EntProtoId, FixedPoint2> available,
        [NotNullWhen(true)] out List<(FoodRecipePrototype Proto, FixedPoint2 Quantity)>? path)
    {
        path = new();

        var query = new Dictionary<EntProtoId, FixedPoint2>(available);
        var recipes = new List<Dictionary<FoodRecipePrototype, FixedPoint2>>();
        var queue = new Queue<(int Depth, FoodRecipePrototype Proto, FixedPoint2 Quantity)>();

        queue.Enqueue((0, target, 1));

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
                path.Add((proto, quan));
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
}
