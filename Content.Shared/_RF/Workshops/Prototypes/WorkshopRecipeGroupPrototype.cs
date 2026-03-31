using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Workshops.Prototypes;

/// <summary>
/// Prototype of the workshop recipe category.
/// </summary>
[Prototype]
public sealed class WorkshopRecipeGroupPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; } = default!;

    public string Name => Loc.GetString($"workshop-recipe-group-{ID.ToLowerInvariant()}-name");

    /// <summary>
    /// An entity prototype that will be used as the icon for this category in the UI.
    /// </summary>
    [DataField]
    public EntProtoId? IconEntity;

    [DataField]
    public List<ProtoId<WorkshopRecipeGroupPrototype>> SubGroups = new();
}
