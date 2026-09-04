using Content.Shared.Alert;
using Content.Shared.Destructible.Thresholds;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Needs.Prototypes;

/// <summary>
/// This is a prototype for an entity's need
/// </summary>
[Prototype]
public sealed partial class NeedPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Need category.
    /// </summary>
    [DataField]
    public ProtoId<NeedCategoryPrototype> Category;

    /// <summary>
    /// A dictionary with threshold values of satisfaction of a need and their IDs.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<NeedThresholdPrototype>> Thresholds = new();

    /// <summary>
    /// Alert category prototype.
    /// </summary>
    [DataField]
    public ProtoId<AlertCategoryPrototype>? AlertCategory;

    /// <summary>
    /// The time between each threshold update.
    /// </summary>
    [DataField]
    public TimeSpan ThresholdUpdateRate = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Minimum and maximum values for randomizing the initial value of satisfaction of a need.
    /// </summary>
    [DataField]
    public MinMax? RoundstartRandomize;
}
