using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Workshops.Prototypes;

/// <summary>
/// Prototype of the workshop's recipe table.
/// </summary>
[Prototype]
public sealed partial class WorkshopRecipeTablePrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public List<ProtoId<WorkshopRecipePrototype>> Recipes = new();

    [DataField]
    public List<ProtoId<WorkshopRecipeTablePrototype>> Tables = new();
}
