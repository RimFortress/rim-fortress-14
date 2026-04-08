using System.Collections.Frozen;
using System.Linq;
using Content.Shared._RF.Workshops.Components;
using Content.Shared._RF.Workshops.Prototypes;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Workshops.Systems;

public abstract partial class SharedWorkshopSystem
{
    private FrozenDictionary<EntProtoId, List<ProtoId<WorkshopRecipePrototype>>> _recipes
        = new Dictionary<EntProtoId, List<ProtoId<WorkshopRecipePrototype>>>().ToFrozenDictionary();

    private FrozenDictionary<ProtoId<WorkshopRecipeGroupPrototype>, HashSet<ProtoId<WorkshopRecipeGroupPrototype>>>
        _groupsParents
            = new Dictionary<ProtoId<WorkshopRecipeGroupPrototype>, HashSet<ProtoId<WorkshopRecipeGroupPrototype>>>()
                .ToFrozenDictionary();

    private FrozenDictionary<ProtoId<WorkshopRecipeGroupPrototype>, HashSet<ProtoId<WorkshopRecipePrototype>>>
        _groupRecipes
            = new Dictionary<ProtoId<WorkshopRecipeGroupPrototype>, HashSet<ProtoId<WorkshopRecipePrototype>>>()
                .ToFrozenDictionary();

    private FrozenSet<ProtoId<WorkshopRecipeGroupPrototype>> _nullGroups
        = new HashSet<ProtoId<WorkshopRecipeGroupPrototype>>().ToFrozenSet();
    private FrozenSet<ProtoId<WorkshopRecipePrototype>> _nullRecipes
        = new HashSet<ProtoId<WorkshopRecipePrototype>>().ToFrozenSet();

    private void ReloadPrototypes()
    {
        var recipes = new Dictionary<EntProtoId, List<ProtoId<WorkshopRecipePrototype>>>();
        var groupsParents = new Dictionary<ProtoId<WorkshopRecipeGroupPrototype>, HashSet<ProtoId<WorkshopRecipeGroupPrototype>>>();
        var groupRecipes = new Dictionary<ProtoId<WorkshopRecipeGroupPrototype>, HashSet<ProtoId<WorkshopRecipePrototype>>>();
        var nullGroups = new HashSet<ProtoId<WorkshopRecipeGroupPrototype>>();
        var nullRecipes = new HashSet<ProtoId<WorkshopRecipePrototype>>();

        foreach (var proto in Proto.EnumeratePrototypes<WorkshopRecipePrototype>())
        {
            if (!recipes.ContainsKey(proto.Result))
                recipes[proto.Result] = new();

            recipes[proto.Result].Add(proto);

            if (proto.Group == null)
            {
                nullRecipes.Add(proto);
                continue;
            }

            if (!groupRecipes.TryAdd(proto.Group.Value, new() { proto }))
                groupRecipes[proto.Group.Value].Add(proto);
        }

        foreach (var proto in Proto.EnumeratePrototypes<WorkshopRecipeGroupPrototype>())
        {
            groupsParents[proto] = proto.SubGroups.ToHashSet();
        }

        foreach (var (group, _) in groupsParents)
        {
            var @null = true;

            foreach (var (protoId, list) in groupsParents)
            {
                if (group == protoId || !list.Contains(group))
                    continue;

                @null = false;
                break;
            }

            if (@null)
                nullGroups.Add(group);
        }

        _recipes = recipes.ToFrozenDictionary();
        _groupsParents = groupsParents.ToFrozenDictionary();
        _groupRecipes = groupRecipes.ToFrozenDictionary();
        _nullGroups = nullGroups.ToFrozenSet();
        _nullRecipes = nullRecipes.ToFrozenSet();
    }

    /// <summary>
    /// Returns the parent group for given, if any.
    /// </summary>
    [PublicAPI, Pure]
    public ProtoId<WorkshopRecipeGroupPrototype>? GetParentGroup(ProtoId<WorkshopRecipeGroupPrototype>? protoId)
    {
        if (protoId == null)
            return null;

        foreach (var (group, children) in _groupsParents)
        {
            if (children.Contains(protoId.Value))
                return group;
        }

        return null;
    }

    /// <summary>
    /// Checks whether the recipe is in the workshop recipe table.
    /// </summary>
    [PublicAPI, Pure]
    public bool ContainsRecipe(Entity<WorkshopComponent?> ent, ProtoId<WorkshopRecipePrototype> recipe)
        => Resolve(ent, ref ent.Comp, false) && ContainsRecipe(ent.Comp.Recipes, recipe);

    /// <summary>
    /// Checks whether the recipe is in the recipe table.
    /// </summary>
    [PublicAPI, Pure]
    public bool ContainsRecipe(ProtoId<WorkshopRecipeTablePrototype> protoId, ProtoId<WorkshopRecipePrototype> recipe)
    {
        if (!Proto.Resolve(protoId, out var proto))
            return false;

        if (proto.Recipes.Contains(recipe))
            return true;

        foreach (var subTable in proto.Tables)
        {
            if (ContainsRecipe(subTable, recipe))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Checks whether the table contains a recipe belonging to a given group.
    /// </summary>
    [PublicAPI, Pure]
    public bool ContainsGroup(ProtoId<WorkshopRecipeTablePrototype> protoId, ProtoId<WorkshopRecipeGroupPrototype> group)
    {
        if (!Proto.Resolve(protoId, out var proto))
            return false;

        foreach (var recipeId in proto.Recipes)
        {
            if (Proto.Resolve(recipeId, out var recipe) && recipe.Group == group)
                return true;
        }

        foreach (var subTable in proto.Tables)
        {
            if (ContainsGroup(subTable, group))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Returns all recipes from the recipes table.
    /// </summary>
    [PublicAPI, Pure]
    public HashSet<ProtoId<WorkshopRecipePrototype>> GetTableRecipes(ProtoId<WorkshopRecipeTablePrototype> tableId)
    {
        if (!Proto.Resolve(tableId, out var proto))
            return new();

        var tableRecipes = new HashSet<ProtoId<WorkshopRecipePrototype>>();

        foreach (var recipe in proto.Recipes)
        {
            tableRecipes.Add(recipe);
        }

        foreach (var table in proto.Tables)
        {
            tableRecipes.UnionWith(GetTableRecipes(table));
        }

        return tableRecipes;
    }

    /// <summary>
    /// Returns all recipe groups from the recipes table.
    /// </summary>
    [PublicAPI, Pure]
    public HashSet<ProtoId<WorkshopRecipeGroupPrototype>> GetTableGroups(ProtoId<WorkshopRecipeTablePrototype> tableId)
    {
        if (!Proto.Resolve(tableId, out var proto))
            return new();

        var tableGroups = new HashSet<ProtoId<WorkshopRecipeGroupPrototype>>();

        foreach (var recipeId in proto.Recipes)
        {
            if (Proto.Resolve(recipeId, out var recipe) && recipe.Group != null)
                tableGroups.Add(recipe.Group.Value);
        }

        foreach (var table in proto.Tables)
        {
            tableGroups.UnionWith(GetTableGroups(table));
        }

        return tableGroups;
    }

    /// <summary>
    /// Returns the parent group for given, if any.
    /// </summary>
    [PublicAPI, Pure]
    public ProtoId<WorkshopRecipeGroupPrototype>? GetParentGroup(
        ProtoId<WorkshopRecipeGroupPrototype>? protoId,
        ProtoId<WorkshopRecipeTablePrototype> tableId)
    {
        if (protoId == null)
            return null;

        var groups = GetTableGroups(tableId);

        foreach (var (group, children) in _groupsParents)
        {
            if (children.Contains(protoId.Value) && groups.Contains(group))
                return group;
        }

        return null;
    }

    [PublicAPI, Pure]
    public HashSet<ProtoId<WorkshopRecipePrototype>> GetGroupRecipes(
        ProtoId<WorkshopRecipeGroupPrototype>? protoId,
        HashSet<ProtoId<WorkshopRecipePrototype>> recipes)
    {
        if (protoId == null || !_groupRecipes.TryGetValue(protoId.Value, out var groupRecipes))
            groupRecipes = _nullRecipes.ToHashSet();

        var tableRecipes = new HashSet<ProtoId<WorkshopRecipePrototype>>();

        foreach (var recipe in recipes)
        {
            if (groupRecipes.Contains(recipe))
                tableRecipes.Add(recipe);
        }

        return tableRecipes;
    }

    [PublicAPI, Pure]
    public HashSet<ProtoId<WorkshopRecipeGroupPrototype>> GetChildrenGroups(
        ProtoId<WorkshopRecipeGroupPrototype>? protoId,
        ProtoId<WorkshopRecipeTablePrototype> tableId)
    {
        if (protoId == null || !_groupsParents.TryGetValue(protoId.Value, out var children))
            children = _nullGroups.ToHashSet();

        var groups = new HashSet<ProtoId<WorkshopRecipeGroupPrototype>>();

        foreach (var group in GetTableGroups(tableId))
        {
            if (protoId != group && children.Contains(group))
                groups.Add(group);
        }

        return groups;
    }

    [PublicAPI, Pure]
    public int GetGroupCount(
        ProtoId<WorkshopRecipeGroupPrototype> protoId,
        HashSet<ProtoId<WorkshopRecipePrototype>> recipes)
        => !_groupRecipes.TryGetValue(protoId, out var groupRecipes) ? 0 : groupRecipes.Count(recipes.Contains);
}
