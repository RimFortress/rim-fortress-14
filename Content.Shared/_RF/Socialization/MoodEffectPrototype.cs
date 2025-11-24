using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Socialization;

/// <summary>
/// Prototype of an effect influencing the mood of an entity
/// </summary>
[Prototype]
public sealed partial class MoodEffectPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; } = default!;

    /// <summary>
    /// Name of the effect that affects mood
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
    /// How much this effect changes mood
    /// </summary>
    [DataField]
    public int Effect;
}
