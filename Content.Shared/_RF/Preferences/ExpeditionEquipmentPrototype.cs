using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Preferences;

/// <summary>
/// A prototype containing items available for selection as expedition starter equipment
/// </summary>
[Prototype]
public sealed partial class ExpeditionEquipmentPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; } = default!;

    /// <summary>
    /// Name of equipment category, supports localization
    /// </summary>
    [DataField(required: true)]
    public string Name { get; } = string.Empty;

    /// <summary>
    /// Whether this category should be hidden in the lobby
    /// </summary>
    [DataField]
    public bool Hidden { get; }

    /// <summary>
    /// Dictionary with the value of each category item in points
    /// </summary>
    [DataField(required: true)]
    public Dictionary<EntProtoId, int> Items { get; } = new();
}
