using Content.Shared.Alert;
using Content.Shared.Destructible.Thresholds;
using Content.Shared.StatusIcon;
using Robust.Shared.Prototypes;

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
    /// Decay time to the next threshold for each threshold
    /// </summary>
    /// <remarks>
    /// It is calculated in world time, which is then converted to simulation time.
    /// </remarks>
    /// <seealso cref="Content.Shared._RF.World.SharedRimFortressWorldSystem.FromWorldTime(TimeSpan)"/>
    [DataField]
    public Dictionary<string, TimeSpan> ThresholdDecayTime = new();

    /// <summary>
    /// A dictionary with threshold values of satisfaction of a need and their IDs
    /// </summary>
    [DataField]
    public Dictionary<string, float> Thresholds = new();

    /// <summary>
    /// A dictionary with alerts for different thresholds
    /// </summary>
    [DataField]
    public Dictionary<string, ProtoId<AlertPrototype>> ThresholdAlerts = new();

    /// <summary>
    /// Localization ID for each threshold to display in the UI
    /// </summary>
    [DataField]
    public Dictionary<string, LocId> ThresholdLocalization = new();

    /// <summary>
    /// Alert category prototype
    /// </summary>
    [DataField]
    public ProtoId<AlertCategoryPrototype>? AlertCategory;

    /// <summary>
    /// Entity status icons for each threshold of need satisfaction
    /// </summary>
    [DataField]
    public Dictionary<string, ProtoId<SatiationIconPrototype>> ThresholdStatusIcons = new();

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
