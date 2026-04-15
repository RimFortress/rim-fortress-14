using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;

namespace Content.Shared._RF.Workshops.Prototypes;

/// <summary>
/// Prototype of the workshop recipe category.
/// </summary>
[Prototype]
public sealed partial class WorkshopRecipeGroupPrototype : IPrototype, IInheritingPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = string.Empty;

    /// <inheritdoc/>
    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<WorkshopRecipeGroupPrototype>))]
    public string[]? Parents { get; private set; }

    /// <inheritdoc/>
    [NeverPushInheritance]
    [AbstractDataField]
    public bool Abstract { get; private set; }

    public string Name => Loc.GetString($"workshop-recipe-group-{ID.ToLowerInvariant()}-name");

    /// <summary>
    /// An entity prototype that will be used as the icon for this category in the UI.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId IconEntity;

    [DataField]
    public List<ProtoId<WorkshopRecipeGroupPrototype>> SubGroups = new();
}
