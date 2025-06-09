using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Stockpile;

/// <summary>
/// Indicates that the entity belongs to the stock item category
/// </summary>
[RegisterComponent]
public sealed partial class StockpileCategoryComponent : Component
{
    [DataField(required: true)]
    public ProtoId<StockpileCategoryPrototype> Category;
}
