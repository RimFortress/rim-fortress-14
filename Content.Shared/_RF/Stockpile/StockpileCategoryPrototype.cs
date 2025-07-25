using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._RF.Stockpile;

/// <summary>
/// Prototype category for customizing storage items in the stockpile
/// </summary>
[Prototype]
public sealed class StockpileCategoryPrototype : IPrototype, ISerializationHooks
{
    private ILocalizationManager _loc = default!;

    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; } = default!;

    [DataField("name")]
    private string? _name;

    public string Name => _loc.TryGetString($"stockpile-category-{ID}-name", out var name) ? name : _name ?? ID;

    [DataField("icon", required: true)]
    public SpriteSpecifier Icon = default!;

    [DataField]
    public List<ProtoId<StockpileCategoryPrototype>> SubCategories = new();

    /// <inheritdoc/>
    void ISerializationHooks.AfterDeserialization()
    {
        _loc = IoCManager.Resolve<ILocalizationManager>();
    }
}
