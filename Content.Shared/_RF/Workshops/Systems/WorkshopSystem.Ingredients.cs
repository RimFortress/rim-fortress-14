using Content.Shared._RF.Workshops.Components;
using Content.Shared._RF.Workshops.Prototypes;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Stacks;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Workshops.Systems;

public sealed partial class WorkshopSystem
{
    /// <summary>
    /// Returns all the ingredients needed to create the recipe.
    /// If the ingredients are recipes themselves,
    /// the ingredients for those recipes will also be included in the returned list.
    /// </summary>
    [PublicAPI, Pure]
    public WorkshopRecipeIngredients GetRecipeIngredients(
        ProtoId<WorkshopRecipePrototype> protoId,
        ProtoId<WorkshopRecipeTablePrototype> tableId)
    {
        if (!_proto.Resolve(protoId, out var proto))
            return new();

        var ingredients = new WorkshopRecipeIngredients(proto.Ingredients);
        var path = GetRecipePath(protoId, tableId);

        foreach (var recipeId in path)
        {
            if (!_proto.Resolve(recipeId, out var recipe))
                continue;

            if (ingredients.Items.TryGetValue(recipe.Result, out var count))
            {
                if (count == 1)
                    ingredients.Items.Remove(recipe.Result);
                else
                    ingredients.Items[recipe.Result]--;
            }

            ingredients = ingredients.UnionWith(recipe.Ingredients);
        }

        return ingredients;
    }

    private void DeleteIngredients(WorkshopComponent comp, WorkshopRecipePrototype proto)
    {
        var ingredients = new WorkshopRecipeIngredients(proto.Ingredients);

        foreach (var uid in comp.ContentStorage.ContainedEntities)
        {
            if (_stackQuery.TryComp(uid, out var stack)
                && ingredients.Materials.TryGetValue(stack.StackTypeId, out var stackCount))
            {
                if (stack.Count == stackCount)
                {
                    QueueDel(uid);
                    ingredients.Materials.Remove(stack.StackTypeId);
                    continue;
                }

                if (stack.Count > stackCount)
                {
                    _stack.SetCount(new(uid, stack), stack.Count - stackCount);
                    ingredients.Materials.Remove(stack.StackTypeId);
                }
                else
                {
                    QueueDel(uid);
                    ingredients.Materials[stack.StackTypeId] -= stack.Count;
                    continue;
                }
            }

            if (Prototype(uid) is { } entProto
                && ingredients.Items.TryGetValue(entProto, out var itemCount))
            {
                QueueDel(uid);

                if (itemCount <= 1)
                    ingredients.Items.Remove(entProto);
                else
                    ingredients.Items[entProto]--;

                continue;
            }

            if (!_solution.TryGetDrainableSolution(uid, out var solutionEntity, out var solution))
                continue;

            foreach (var (reagent, _) in ingredients.Reagents)
            {
                // removed everything
                if (!ingredients.Reagents.TryGetValue(reagent, out var reagentCount))
                    continue;

                var quant = solution.GetTotalPrototypeQuantity(reagent);

                if (quant >= reagentCount)
                {
                    quant = reagentCount;
                    ingredients.Reagents.Remove(reagent);
                }
                else
                    ingredients.Reagents[reagent] -= quant;

                _solution.RemoveReagent(solutionEntity.Value, reagent, quant);
            }
        }
    }

    private void GetIngredients(
        Entity<WorkshopComponent?> ent,
        out WorkshopRecipeIngredients ingredients)
    {
        ingredients = new();

        if (!Resolve(ent, ref ent.Comp))
            return;

        GetIngredients(ent.Comp, out ingredients.Materials, out ingredients.Items, out ingredients.Reagents);
    }

    private void GetIngredients(
        WorkshopComponent comp,
        out Dictionary<ProtoId<StackPrototype>, int> materials,
        out Dictionary<EntProtoId, int> items,
        out Dictionary<ProtoId<ReagentPrototype>, FixedPoint2> reagents)
    {
        materials = new();
        items = new();
        reagents = new();

        foreach (var uid in comp.ContentStorage.ContainedEntities)
        {
            if (EntityManager.IsQueuedForDeletion(uid))
                continue;

            if (Prototype(uid) is { } proto && !items.TryAdd(proto, 1))
                items[proto]++;

            if (_stackQuery.TryComp(uid, out var stack) && !materials.TryAdd(stack.StackTypeId, stack.Count))
                materials[stack.StackTypeId] += stack.Count;

            if (!_solution.TryGetDrainableSolution(uid, out _, out var sol))
                continue;

            foreach (var (reagent, quantity) in sol)
            {
                if (!reagents.TryAdd(reagent.Prototype, quantity))
                    reagents[reagent.Prototype] += quantity;
            }
        }
    }

    /// <summary>
    /// Returns entities from the workshop container that can be used to create the target recipe.
    /// </summary>
    private void GetIngredientsEntities(
        Entity<WorkshopComponent?> ent,
        WorkshopRecipePrototype proto,
        out HashSet<EntityUid> entities)
    {
        entities = new();

        if (!Resolve(ent, ref ent.Comp))
            return;

        var ingredients = new WorkshopRecipeIngredients(proto.Ingredients);

        foreach (var uid in ent.Comp.ContentStorage.ContainedEntities)
        {
            if (EntityManager.IsQueuedForDeletion(uid))
                continue;

            if (Prototype(uid) is { } entProto
                && ingredients.Items.GetValueOrDefault(entProto, 0) > 0)
            {
                ingredients.Items[entProto]--;
                entities.Add(uid);
            }

            if (_stackQuery.TryComp(uid, out var stack)
                && ingredients.Materials.GetValueOrDefault(stack.StackTypeId, 0) > 0)
            {
                ingredients.Materials[stack.StackTypeId] -= stack.Count;
                entities.Add(uid);
            }

            if (!_solution.TryGetDrainableSolution(uid, out _, out var sol))
                continue;

            foreach (var (reagent, quantity) in sol)
            {
                if (ingredients.Reagents.GetValueOrDefault(reagent.Prototype, 0) <= 0)
                    continue;

                ingredients.Reagents[reagent.Prototype] -= quantity;
                entities.Add(uid);
            }
        }
    }

    /// <summary>
    /// Returns a list of remaining ingredients needed to create the target recipe.
    /// </summary>
    /// <param name="ent">Workshop entity.</param>
    /// <param name="protoId">Target recipe prototype.</param>
    [PublicAPI, Pure]
    public WorkshopRecipeIngredients GetRemainingIngredients(
        Entity<WorkshopComponent?> ent,
        ProtoId<WorkshopRecipePrototype> protoId)
    {
        if (!Resolve(ent, ref ent.Comp)
            || !_proto.Resolve(protoId, out var proto))
            return new();

        var ingredients = new WorkshopRecipeIngredients(proto.Ingredients);

        foreach (var uid in ent.Comp.ContentStorage.ContainedEntities)
        {
            if (EntityManager.IsQueuedForDeletion(uid))
                continue;

            if (_stackQuery.TryComp(uid, out var stack)
                && ingredients.Materials.TryGetValue(stack.StackTypeId, out var stackCount))
            {
                if (stack.Count == stackCount)
                {
                    ingredients.Materials.Remove(stack.StackTypeId);
                    continue;
                }

                if (stack.Count > stackCount)
                    ingredients.Materials.Remove(stack.StackTypeId);
                else
                {
                    ingredients.Materials[stack.StackTypeId] -= stack.Count;
                    continue;
                }
            }

            if (Prototype(uid) is { } entProto && ingredients.Items.TryGetValue(entProto, out var count))
            {
                if (count <= 1)
                    ingredients.Items.Remove(entProto);
                else
                    ingredients.Items[entProto]--;
            }

            if (!_solution.TryGetDrainableSolution(uid, out _, out var solution))
                continue;

            foreach (var (reagent, _) in ingredients.Reagents)
            {
                // removed everything
                if (!ingredients.Reagents.TryGetValue(reagent, out var reagentCount))
                    continue;

                var quant = solution.GetTotalPrototypeQuantity(reagent);

                if (quant >= reagentCount)
                    ingredients.Reagents.Remove(reagent);
                else
                    ingredients.Reagents[reagent] -= quant;
            }
        }

        return ingredients;
    }
}
