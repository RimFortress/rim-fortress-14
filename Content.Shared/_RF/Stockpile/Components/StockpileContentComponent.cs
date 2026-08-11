using Content.Shared._RF.Stockpile.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared._RF.Stockpile.Components;

/// <summary>
/// Used to indicate an item that is in stock.
/// </summary>
[Access(typeof(StockpileSystem))]
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class StockpileContentComponent : Component
{
    /// <summary>
    /// The stockpile entity where this entity is located.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public EntityUid Stock;
}
