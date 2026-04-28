using Robust.Shared.Prototypes;

namespace Content.Shared._RF.NPC.Search.Prototypes;

/// <summary>
/// A prototype for entity search query.
/// </summary>
[Prototype]
public sealed partial class SearchQueryPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Time during which the results of this search query will be considered valid.
    /// </summary>
    [DataField]
    public TimeSpan ValidTime = TimeSpan.FromSeconds(10f);

    /// <summary>
    /// A search query whose results will be filtered and scored.
    /// </summary>
    [DataField(required: true, serverOnly: true)]
    public SearchQuery Query = default!;

    /// <summary>
    /// Search query filters.
    /// </summary>
    [DataField(serverOnly: true)]
    public List<SearchFilter> Filters = new();

    /// <summary>
    /// Search query considerations.
    /// </summary>
    [DataField(serverOnly: true)]
    public List<SearchConsideration> Considerations = new();
}
