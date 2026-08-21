using Robust.Shared.Prototypes;

namespace Content.Shared._RF.NPC.Search.Prototypes;

/// <summary>
/// This is a prototype of a filter preset that can be reused via <c>!type:Preset</c>.
/// </summary>
[Prototype]
public sealed partial class SearchFiltersPresetPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; set; } = default!;

    /// <summary>
    /// Filters list.
    /// </summary>
    [DataField(required: true, serverOnly: true)]
    public List<SearchFilter> Filters = new();
}
