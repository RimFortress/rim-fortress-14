using Content.Shared._RF.Workshops.Components;
using Content.Shared._RF.Workshops.Prototypes;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Stacks;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Workshops.Systems;

public abstract partial class SharedWorkshopSystem
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
        if (!Proto.Resolve(protoId, out var proto))
            return new();

        var ingredients = new WorkshopRecipeIngredients(proto.Ingredients);
        var path = GetRecipePath(protoId, tableId);

        foreach (var recipeId in path)
        {
            if (!Proto.Resolve(recipeId, out var recipe))
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

    protected void DeleteIngredients(WorkshopComponent comp, WorkshopRecipePrototype proto)
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
                    _stack.SetCount(uid, stack.Count - stackCount, stack);
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

            if (!Solution.TryGetDrainableSolution(uid, out var solutionEntity, out var solution))
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

                Solution.RemoveReagent(solutionEntity.Value, reagent, quant);
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

            if (!Solution.TryGetDrainableSolution(uid, out _, out var sol))
                continue;

            foreach (var (reagent, quantity) in sol)
            {
                if (!reagents.TryAdd(reagent.Prototype, quantity))
                    reagents[reagent.Prototype] += quantity;
            }
        }
    }

    protected WorkshopRecipeIngredients GetRemainingIngredients(
        WorkshopComponent comp,
        ProtoId<WorkshopRecipePrototype> protoId)
    {
        if (!Proto.Resolve(protoId, out var proto))
            return new();

        var ingredients = new WorkshopRecipeIngredients(proto.Ingredients);

        foreach (var uid in comp.ContentStorage.ContainedEntities)
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

            if (Prototype(uid) is { } ent && ingredients.Items.TryGetValue(ent, out var count))
            {
                if (count <= 1)
                    ingredients.Items.Remove(ent);
                else
                    ingredients.Items[ent]--;
            }

            if (!Solution.TryGetDrainableSolution(uid, out _, out var solution))
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
