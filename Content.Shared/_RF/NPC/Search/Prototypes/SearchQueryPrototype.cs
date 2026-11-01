using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;

namespace Content.Shared._RF.NPC.Search.Prototypes;

/// <summary>
/// A prototype for entity search query.
/// </summary>
[Prototype]
public sealed partial class SearchQueryPrototype : IPrototype, IInheritingPrototype
{
    /// <inheritdoc />
    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<SearchQueryPrototype>))]
    public string[]? Parents { get; private set; }

    /// <inheritdoc />
    [AbstractDataField, NeverPushInheritance]
    public bool Abstract { get; private set; }

    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Tell the agent when an entity gets into the result of this query, or leaves it,
    /// with <see cref="SearchResultChangedEvent"/>. It costs an event on every change,
    /// so only the queries that are listened to have it.
    /// </summary>
    [DataField]
    public bool Notify;

    /// <summary>
    /// A search query whose results will be filtered and scored.
    /// </summary>
    [DataField(required: true, serverOnly: true)]
    public SearchQuery Query = default!;

    /// <summary>
    /// Search query filters.
    /// </summary>
    [DataField(serverOnly: true), AlwaysPushInheritance]
    public List<SearchFilter> Filters = new();

    /// <summary>
    /// Search query considerations.
    /// </summary>
    [DataField(serverOnly: true), AlwaysPushInheritance]
    public List<SearchConsideration> Considerations = new();
}
