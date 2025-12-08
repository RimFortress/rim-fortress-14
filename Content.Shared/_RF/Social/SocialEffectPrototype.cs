using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Social;

/// <summary>
/// Prototype of the effect that influences the social interactions of entities
/// </summary>
[Prototype]
public sealed partial class SocialEffectPrototype : IPrototype
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
    /// Duration of effect in world time
    /// </summary>
    [DataField]
    public TimeSpan? Duration;

    /// <summary>
    /// How much does the effect change any value
    /// </summary>
    [DataField]
    public int Effect;

    /// <summary>
    /// Maximum effect value when issued several times
    /// </summary>
    [DataField]
    public int MaxEffect = int.MaxValue;
}
