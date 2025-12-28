using Content.Shared.Alert;
using Content.Shared.Destructible.Thresholds;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._RF.Needs;

/// <summary>
/// This is a prototype for an entity's need
/// </summary>
[Prototype]
public sealed partial class NeedPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; } = default!;

    /// <summary>
    /// A dictionary with threshold values of satisfaction of a need and their IDs
    /// </summary>
    [DataField]
    public List<NeedThreshold> Thresholds = new();

    /// <summary>
    /// Alert category prototype
    /// </summary>
    [DataField]
    public ProtoId<AlertCategoryPrototype>? AlertCategory;

    /// <summary>
    /// The time between each threshold update
    /// </summary>
    [DataField]
    public TimeSpan ThresholdUpdateRate = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Minimum and maximum values for randomizing the initial value of satisfaction of a need
    /// </summary>
    [DataField]
    public MinMax? RoundstartRandomize;
}

[DataDefinition]
public sealed partial class NeedThreshold
{
    [DataField(required: true)]
    public string Id;

    [DataField(required: true)]
    public float Value;

    /// <summary>
    /// Decay time to the next threshold
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
    /// Threshold alert category prototype
    /// </summary>
    [DataField]
    public ProtoId<AlertPrototype>? Alert;

    /// <summary>
    /// Localization ID for the threshold to display in the UI
    /// </summary>
    [DataField]
    public LocId? Description;
}
