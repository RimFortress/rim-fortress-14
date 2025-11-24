using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Socialization;

/// <summary>
/// A prototype of the effect that changes an entity's opinion of another entity
/// </summary>
[Prototype]
public sealed partial class OpinionEffectsPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; } = default!;

    /// <summary>
    /// Name of the effect
    /// </summary>
    [DataField]
    public LocId Name;

    [DataField]
    public LocId? Description;

    /// <summary>
    /// Duration of effect
    /// </summary>
    [DataField]
    public TimeSpan? Duration;

    /// <summary>
    /// How much this effect changes opinion level
    /// </summary>
    [DataField]
    public int Effect;

    /// <summary>
    /// Tags that will be added to the opinion of an entity to another
    /// </summary>
    [DataField]
    public List<ProtoId<TagPrototype>> Tags = new();
}
