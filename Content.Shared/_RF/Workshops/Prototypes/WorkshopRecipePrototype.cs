using Content.Shared._RF.Skills.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Stacks;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;

namespace Content.Shared._RF.Workshops.Prototypes;

/// <summary>
/// A prototype of a recipe that can be crafted in a workshop.
/// </summary>
[Prototype]
public sealed partial class WorkshopRecipePrototype : IPrototype, IInheritingPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <inheritdoc/>
    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<WorkshopRecipePrototype>))]
    public string[]? Parents { get; private set; }

    /// <inheritdoc/>
    [NeverPushInheritance]
    [AbstractDataField]
    public bool Abstract { get; private set; }

    /// <summary>
    /// The result of producing this recipe.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Result;

    /// <summary>
    /// The group to which this recipe belongs. Used in the UI.
    /// </summary>
    [DataField]
    public ProtoId<WorkshopRecipeGroupPrototype>? Group;

    /// <summary>
    /// All the ingredients needed to make this recipe.
    /// </summary>
    [DataField(required: true)]
    public WorkshopRecipeIngredients Ingredients;

    /// <summary>
    /// The time it will take to produce this recipe.
    /// </summary>
    [DataField]
    public TimeSpan CraftingTime = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Experience for what skills will the user gain after completing this recipe.
    /// </summary>
    [DataField]
    public List<SkillExperience> SkillsUp = new();
}

[DataDefinition]
public partial struct WorkshopRecipeIngredients
{
    /// <summary>
    /// Materials needed to complete the recipe.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<StackPrototype>, int> Materials = new();

    /// <summary>
    /// Items needed to complete the recipe.
    /// </summary>
    [DataField]
    public Dictionary<EntProtoId, int> Items = new();

    /// <summary>
    /// Chemical reagents needed to complete the recipe.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<ReagentPrototype>, FixedPoint2> Reagents = new();

    [Pure, PublicAPI]
    public bool Satisfied(WorkshopRecipeIngredients other)
    {
        foreach (var (stack, count) in Materials)
        {
            if (!other.Materials.TryGetValue(stack, out var current) || current < count)
                return false;
        }

        foreach (var (item, count) in Items)
        {
            if (!other.Items.TryGetValue(item, out var current) || current < count)
                return false;
        }

        foreach (var (reagent, count) in Reagents)
        {
            if (!other.Reagents.TryGetValue(reagent, out var current) || current < count)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Returns a new list of ingredients created by merging two other lists.
    /// </summary>
    [Pure, PublicAPI]
    public WorkshopRecipeIngredients UnionWith(WorkshopRecipeIngredients other)
    {
        var ingredients = new WorkshopRecipeIngredients(this);

        foreach (var (stack, count) in other.Materials)
        {
            if (!ingredients.Materials.TryAdd(stack, count))
                ingredients.Materials[stack] += count;
        }

        foreach (var (item, count) in other.Items)
        {
            if (!ingredients.Items.TryAdd(item, count))
                ingredients.Items[item] += count;
        }

        foreach (var (reagent, count) in other.Reagents)
        {
            if (!ingredients.Reagents.TryAdd(reagent, count))
                ingredients.Reagents[reagent] += count;
        }

        return ingredients;
    }

    [PublicAPI, Pure]
    public int IngredientCount()
    {
        var ingredientCount = Reagents.Count;

        foreach (var (_, count) in Materials)
        {
            ingredientCount += count;
        }

        foreach (var (_, count) in Items)
        {
            ingredientCount += count;
        }

        return ingredientCount;
    }

    public WorkshopRecipeIngredients(WorkshopRecipeIngredients other)
    {
        Materials = new(other.Materials);
        Items = new(other.Items);
        Reagents = new(other.Reagents);
    }
}
