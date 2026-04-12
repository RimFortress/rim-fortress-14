using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;

namespace Content.Shared._RF.Workshops.Prototypes;

/// <summary>
/// Prototype of the workshop recipe category.
/// </summary>
[Prototype]
public sealed class WorkshopRecipeGroupPrototype : IPrototype, IInheritingPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; } = default!;

    /// <inheritdoc/>
    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<WorkshopRecipeGroupPrototype>))]
    public string[]? Parents { get; }

    /// <inheritdoc/>
    [NeverPushInheritance]
    [AbstractDataField]
    public bool Abstract { get; }

    public string Name => Loc.GetString($"workshop-recipe-group-{ID.ToLowerInvariant()}-name");

    /// <summary>
    /// An entity prototype that will be used as the icon for this category in the UI.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId IconEntity;

    [DataField]
    public List<ProtoId<WorkshopRecipeGroupPrototype>> SubGroups = new();
}
