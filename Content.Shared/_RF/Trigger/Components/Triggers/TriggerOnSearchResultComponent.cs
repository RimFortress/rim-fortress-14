using Content.Shared._RF.NPC.Search;

namespace Content.Shared._RF.Trigger.Components.Triggers;

/// <summary>
/// Triggers the entity when an entity gets into the result of a search query of the entity, or leaves it, with the keys
/// <c>QUERY.added</c> and <c>QUERY.removed</c>, the user is the entity. Only the queries that have <c>notify</c> do it,
/// see <see cref="SearchResultChangedEvent"/>.
/// </summary>
[RegisterComponent]
public sealed partial class TriggerOnSearchResultComponent : Component;
