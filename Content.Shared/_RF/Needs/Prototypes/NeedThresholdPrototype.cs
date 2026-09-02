using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Needs.Prototypes;

/// <summary>
/// A prototype that exists only to validate the ID thresholds of
/// needs prototypes and the ability to specify the same ID threshold for different needs.
/// </summary>
[Prototype]
public sealed partial class NeedThresholdPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; set; } = default!;
}
