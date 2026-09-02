using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Needs.Prototypes;

/// <summary>
/// A needs category prototype used to work with needs
/// thresholds without needing to know the specific needs prototype.
/// </summary>
[Prototype]
public sealed partial class NeedCategoryPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; set; } = default!;
}
