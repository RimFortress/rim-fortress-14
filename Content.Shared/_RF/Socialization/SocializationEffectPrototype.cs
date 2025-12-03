using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Socialization;

/// <summary>
/// Prototype of the effect that influences the social interactions of entities
/// </summary>
[Prototype]
public sealed partial class SocializationEffectPrototype : IPrototype
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
    /// How much does the effect change any value
    /// </summary>
    [DataField]
    public int Effect;

    /// <summary>
    /// Tags that will be added to the opinion of an entity to another
    /// </summary>
    [DataField]
    public List<ProtoId<TagPrototype>> Tags = new();

    /// <summary>
    /// Can this effect be issued multiple times
    /// </summary>
    [DataField]
    public bool Multiply;

    /// <summary>
    /// Maximum value of the effect that can be achieved by multiplying the effect
    /// </summary>
    [DataField]
    public int MaxMultiplier = int.MaxValue;
}
