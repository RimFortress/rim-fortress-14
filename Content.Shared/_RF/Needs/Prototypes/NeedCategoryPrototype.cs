using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

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

    public LocId Name => $"need-{CaseConversion.PascalToKebab(ID)}-name";
}
