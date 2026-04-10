using Robust.Shared.GameStates;

namespace Content.Shared._RF.Stockpile;

/// <summary>
/// This is used to supply stockpiles from the containers' contents.
/// </summary>
[Access(typeof(ContainerStockSupplierSystem))]
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ContainerStockSupplierComponent : Component
{
    /// <summary>
    /// ID of containers supplying stockpiles.
    /// </summary>
    [DataField]
    public List<string> Containers = new();

    [ViewVariables, AutoNetworkedField]
    public HashSet<int> Supplied = new();
}
