using Content.Shared.Alert;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;
using Robust.Shared.Utility;

namespace Content.Shared._RF.Needs.Prototypes;

/// <summary>
/// A prototype threshold value for need.
/// </summary>
[Prototype]
public sealed partial class NeedThresholdPrototype : IPrototype, IInheritingPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; set; } = default!;

    /// <inheritdoc/>
    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<NeedThresholdPrototype>))]
    public string[]? Parents { get; set; }

    /// <inheritdoc/>
    [NeverPushInheritance]
    [AbstractDataField]
    public bool Abstract { get; set; }

    /// <summary>
    /// Need threshold category.
    /// </summary>
    [DataField]
    public ProtoId<NeedThresholdCategoryPrototype> Category;

    [DataField(required: true)]
    public float Value;

    /// <summary>
    /// Decay time to the next threshold.
    /// </summary>
    /// <remarks>
    /// It is calculated in world time, which is then converted to simulation time.
    /// </remarks>
    /// <seealso cref="Content.Shared._RF.World.SharedRimFortressWorldSystem.FromWorldTime(TimeSpan)"/>
    [DataField(required: true)]
    public TimeSpan DecayTime;

    [DataField]
    public SpriteSpecifier? Icon;

    /// <summary>
    /// Threshold alert category prototype.
    /// </summary>
    [DataField]
    public ProtoId<AlertPrototype>? Alert;

    /// <summary>
    /// Effects that will be applied to the entity when a threshold is set.
    /// </summary>
    [DataField]
    public EntityEffect[] Effects = Array.Empty<EntityEffect>();

    /// <summary>
    /// Effects that are applied with every update of this threshold.
    /// </summary>
    [DataField]
    public EntityEffect[] TickEffects = Array.Empty<EntityEffect>();
}
