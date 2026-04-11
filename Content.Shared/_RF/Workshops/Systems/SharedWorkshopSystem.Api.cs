using System.Diagnostics.CodeAnalysis;
using Content.Shared._RF.Workshops.Components;
using Content.Shared._RF.Workshops.Prototypes;
using JetBrains.Annotations;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._RF.Workshops.Systems;

public abstract partial class SharedWorkshopSystem
{
    /// <summary>
    /// Checks if all the ingredients for the target recipe are available in the workshop.
    /// </summary>
    [PublicAPI, Pure]
    public bool CanCraft(Entity<WorkshopComponent?> ent, ProtoId<WorkshopRecipePrototype> protoId)
    {
        if (!Resolve(ent, ref ent.Comp)
            || !Proto.Resolve(protoId, out var proto)
            || !ContainsRecipe(ent, protoId))
            return false;

        GetIngredients(ent, out var ingredients);
        return proto.Ingredients.Satisfied(ingredients);
    }

    /// <summary>
    /// Returns the recipe from the workshop queue with the specified index.
    /// </summary>
    [PublicAPI, Pure]
    public static ProtoId<WorkshopRecipePrototype>? GetQueueRecipe(WorkshopComponent comp, int index)
    {
        if (index < 0 || index >= comp.Queue.Count)
            return null;

        return comp.Queue.Queue[index].Current;
    }

    /// <summary>
    /// Returns the first recipe from the workshop queue.
    /// </summary>
    [PublicAPI, Pure]
    public ProtoId<WorkshopRecipePrototype>? GetCurrentRecipe(Entity<WorkshopComponent?> ent)
        => !Resolve(ent, ref ent.Comp) ? null : ent.Comp.Queue.Recipe;

    /// <summary>
    /// Builds a path from recipes to create the target recipe.
    /// </summary>
    /// <param name="protoId">Target recipe.</param>
    /// <param name="tableId">Available recipes table.</param>
    /// <returns>A recipe path.</returns>
    [PublicAPI, Pure]
    public ProtoId<WorkshopRecipePrototype>[] GetRecipePath(
        ProtoId<WorkshopRecipePrototype> protoId,
        ProtoId<WorkshopRecipeTablePrototype> tableId)
    {
        var path = new List<ProtoId<WorkshopRecipePrototype>>();

        var recipes = new List<Dictionary<ProtoId<WorkshopRecipePrototype>, int>>();
        var queue = new Queue<(int Depth, ProtoId<WorkshopRecipePrototype> Proto, int Quantity)>();
        var tableRecipes = GetTableRecipes(tableId);

        queue.Enqueue((0, protoId, 1));

        while (queue.TryDequeue(out var recipe))
        {
            if (!Proto.Resolve(recipe.Proto, out var proto))
                continue;

            if (recipes.Count <= recipe.Depth)
                recipes.Add(new());

            // If several identical ingredients are used at the same depth, we add their quantities together
            if (!recipes[recipe.Depth].TryAdd(recipe.Proto, recipe.Quantity))
                recipes[recipe.Depth][recipe.Proto] += recipe.Quantity;

            foreach (var (ent, q) in proto.Ingredients.Items)
            {
                // Else, search for a recipe for the ingredient
                if (!_recipes.TryGetValue(ent, out var list))
                    continue;

                if (list.FirstOrNull(tableRecipes.Contains) is { } valid)
                    queue.Enqueue((recipe.Depth + 1, valid, q * recipe.Quantity));
            }
        }

        recipes.RemoveAt(0);

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

        path.Reverse();
        return path.ToArray();
    }

    /// <summary>
    /// Starts crafting the first item in the workshop queue, if all the required items are available.
    /// </summary>
    /// <param name="ent">Workshop entity.</param>
    /// <returns>True, if the creating has been successfully started.</returns>
    [PublicAPI]
    public bool TryStartCrafting(Entity<WorkshopComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp)
            || ent.Comp.Crafting
            || ent.Comp.ResultStorage.Count >= ent.Comp.ResultCapacity
            || GetCurrentRecipe(ent) is not { } protoId
            || !CanCraft(ent, protoId))
            return false;

        Audio.PlayPvs(ent.Comp.StartCraftingSound, ent);
        ent.Comp.PlayingStream =
            Audio.PlayPvs(ent.Comp.LoopingSound, ent, AudioParams.Default.WithLoop(true).WithMaxDistance(5))?.Entity;
        ent.Comp.Queue.SetEndTime(GetCraftingEndTime(ent, protoId));
        DirtyField(ent, nameof(WorkshopComponent.Queue));
        UpdateAppearance(ent);
        UpdateUi(ent);
        return true;
    }

    /// <summary>
    /// Adds the recipe to the workshop crafting queue.
    /// </summary>
    /// <param name="ent">Workshop entity.</param>
    /// <param name="protoId">Recipe prototype.</param>
    /// <returns>True, if the recipe was successfully added.</returns>
    [PublicAPI]
    public bool AddToQueue(Entity<WorkshopComponent?> ent, ProtoId<WorkshopRecipePrototype> protoId)
    {
        if (!Resolve(ent, ref ent.Comp)
            || ent.Comp.Queue.Count >= ent.Comp.MaxQueue
            || !ContainsRecipe(ent, protoId))
            return false;

        ent.Comp.Queue.Add(protoId, GetRecipePath(protoId, ent.Comp.Recipes));
        DirtyField(ent, nameof(WorkshopComponent.Queue));

        AddPassiveTask(ent);

        if (!ent.Comp.Crafting)
            TryStartCrafting(ent);

        UpdateAppearance(ent);
        UpdateUi(ent);
        return true;
    }

    /// <summary>
    /// Removes the recipe from the workshop's crafting queue.
    /// </summary>
    /// <param name="ent">Workshop entity.</param>
    /// <param name="index">Recipe index in the crafting queue.</param>
    /// <returns>True, if the recipe was successfully removed.</returns>
    [PublicAPI]
    public bool RemoveFromQueue(Entity<WorkshopComponent?> ent, int index)
    {
        if (!Resolve(ent, ref ent.Comp)
            || index < 0
            || index >= ent.Comp.Queue.Count)
            return false;

        var removedCurrent = index == ent.Comp.Queue.Index;

        if (removedCurrent)
            StopCrafting(ent);

        ent.Comp.Queue.RemoveAt(index);
        DirtyField(ent, nameof(WorkshopComponent.Queue));

        if (ent.Comp.Queue.Count == 0)
        {
            RemovePassiveTask(ent);
            FinishTask(ent);
            UpdateAppearance(ent);
            UpdateUi(ent);
            return true;
        }

        if (removedCurrent)
        {
            UpdateNpcRecipe(ent.Owner);

            if (TryStartCrafting(ent))
                return true;
        }

        UpdateAppearance(ent);
        UpdateUi(ent);
        return true;
    }

    /// <summary>
    /// Toggles whether a recipe is repeated in the recipe queue.
    /// Repeated recipes are not removed from the queue upon completion.
    /// </summary>
    /// <param name="ent">Workshop entity.</param>
    /// <param name="index">Recipe Index.</param>
    [PublicAPI]
    public void ToggleRepeat(Entity<WorkshopComponent?> ent, int index)
    {
        if (!Resolve(ent, ref ent.Comp)
            || index < 0
            || index >= ent.Comp.Queue.Count)
            return;

        ent.Comp.Queue.Queue[index].Repeat = !ent.Comp.Queue.Queue[index].Repeat;
        DirtyField(ent, nameof(WorkshopComponent.Queue));
        UpdateUi(ent);
    }

    /// <summary>
    /// Suspends or resumes recipe crafting in the workshop.
    /// Suspended recipes are skipped in the recipe queue.
    /// </summary>
    /// <param name="ent">Workshop entity.</param>
    /// <param name="index">Recipe Index.</param>
    [PublicAPI]
    public void ToggleSuspend(Entity<WorkshopComponent?> ent, int index)
    {
        if (!Resolve(ent, ref ent.Comp)
            || index < 0
            || index >= ent.Comp.Queue.Count)
            return;

        SetSuspend(ent, index, !ent.Comp.Queue.Queue[index].Suspended);
    }

    /// <summary>
    /// Suspends or resumes recipe crafting in the workshop.
    /// Suspended recipes are skipped in the recipe queue.
    /// </summary>
    /// <param name="ent">Workshop entity.</param>
    /// <param name="index">Recipe Index.</param>
    /// <param name="suspend"></param>
    [PublicAPI]
    public void SetSuspend(Entity<WorkshopComponent?> ent, int index, bool suspend)
    {
        if (!Resolve(ent, ref ent.Comp)
            || index < 0
            || index >= ent.Comp.Queue.Count)
            return;

        var entry = ent.Comp.Queue.Queue[index];

        if (entry.Suspended == suspend)
            return;

        entry.Suspended = suspend;

        if (entry.Suspended && index == ent.Comp.Queue.Index)
        {
            AdvanceQueue(ent);
            return;
        }

        DirtyField(ent, nameof(WorkshopComponent.Queue));
        UpdateUi(ent);
    }

    /// <summary>
    /// Returns the coordinates of the workspace.
    /// </summary>
    [PublicAPI]
    public EntityCoordinates GetCraftingPlace(Entity<WorkshopComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp))
            return EntityCoordinates.Invalid;

        var xform = Transform(ent);
        var coord = xform.Coordinates;
        var offset = xform.LocalRotation.RotateVec(ent.Comp.CraftingPlace);
        return new EntityCoordinates(coord.EntityId, coord.Position + offset);
    }

    /// <summary>
    /// Returns current workshop NPC user.
    /// </summary>
    [PublicAPI]
    public virtual bool TryGetUser(Entity<WorkshopComponent?> ent, [NotNullWhen(true)] out EntityUid? user)
    {
        user = null;
        return false;
    }

    /// <summary>
    /// Returns current workshop NPC user.
    /// </summary>
    [PublicAPI]
    public virtual EntityUid? GetUser(Entity<WorkshopComponent?> ent)
    {
        return null;
    }
}
