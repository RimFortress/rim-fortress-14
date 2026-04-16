using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Social;

/// <summary>
/// Prototype of the effect that influences the social interactions of entities.
/// </summary>
[Prototype]
public sealed partial class SocialEffectPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Name of the effect.
    /// </summary>
    [DataField]
    public LocId Name;

    [DataField]
    public LocId? Description;

    /// <summary>
    /// Duration of effect in world time.
    /// </summary>
    [DataField]
    public TimeSpan? Duration;

    /// <summary>
    /// How much does the effect change any value.
    /// </summary>
    [DataField]
    public int Effect;

    /// <summary>
    /// Maximum effect value when issued several times.
    /// </summary>
    [DataField]
    public int? MaxEffect;

    /// <summary>
    /// Effects that conflict with this.
    /// </summary>
    [DataField]
    public List<ProtoId<SocialEffectPrototype>> ConflictWith = new();

    /// <summary>
    /// If true, then all effects that conflict with this will be removed, else the effect will not be given.
    /// </summary>
    /// <remarks>
    /// Checking for conflicts takes place only from the side
    /// of the issued effect to those already issued, but not vice versa.
    /// </remarks>
    [DataField]
    public bool RemoveWhenConflict = true;
}
