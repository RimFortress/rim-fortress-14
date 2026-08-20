using Content.Shared._RF.Workshops.Prototypes;
using Content.Shared.DoAfter;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._RF.Workshops;

/// <summary>
/// An event raised when a recipe is added to the workshop's production queue.
/// </summary>
/// <param name="Recipe">Added recipe prototype.</param>
[PublicAPI, ByRefEvent]
public readonly record struct WorkshopQueueAdded(EntityUid Workshop, ProtoId<WorkshopRecipePrototype> Recipe);

/// <summary>
/// An event raised when a recipe is removed from the workshop's production queue.
/// </summary>
/// <param name="Recipe">Removed recipe prototype.</param>
[PublicAPI, ByRefEvent]
public readonly record struct WorkshopQueueRemoved(EntityUid Workshop, ProtoId<WorkshopRecipePrototype> Recipe);

/// <summary>
/// An event raised when a recipe in the workshop's production queue is suspended/resumed.
/// </summary>
/// <param name="Recipe">Suspended/resumed recipe prototype.</param>
/// <param name="Suspended">Was recipe suspended or resumed.</param>
[PublicAPI, ByRefEvent]
public readonly record struct WorkshopRecipeSuspend(EntityUid Workshop, ProtoId<WorkshopRecipePrototype> Recipe, bool Suspended);

/// <summary>
/// An event raised when an ingredient is added to the workshop.
/// </summary>
/// <param name="Workshop">Workshop entity.</param>
/// <param name="Ingredient">Inserted ingredient entity.</param>
[PublicAPI, ByRefEvent]
public readonly record struct WorkshopIngredientInserted(EntityUid Workshop, EntityUid Ingredient);

/// <summary>
/// An event raised when an ingredient is removed from the workshop.
/// </summary>
/// <param name="Workshop">Workshop entity.</param>
/// <param name="Ingredient">Removed ingredient entity.</param>
[PublicAPI, ByRefEvent]
public readonly record struct WorkshopIngredientRemoved(EntityUid Workshop, EntityUid Ingredient);

/// <summary>
/// A recipe crafting event in the workshop.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class WorkshopCraftingDoAfterEvent : DoAfterEvent
{
    /// <summary>
    /// Recipe being created.
    /// </summary>
    [DataField]
    public ProtoId<WorkshopRecipePrototype> Recipe;

    /// <summary>
    /// Ingredients used to make this recipe.
    /// </summary>
    [DataField]
    public HashSet<NetEntity> Ingredients = new();

    public override DoAfterEvent Clone() => this;
}
