using Content.Shared._RF.Stockpile.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Stockpile.Components;

/// <summary>
/// Indicates that the entity belongs to the stock item category
/// </summary>
[RegisterComponent]
public sealed partial class StockpileCategoryComponent : Component
{
    [DataField]
    public ProtoId<StockpileCategoryPrototype>? Category;
}
