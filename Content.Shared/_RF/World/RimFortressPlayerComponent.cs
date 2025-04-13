using Robust.Shared.GameStates;

namespace Content.Shared._RF.World;

/// <summary>
/// Represents the entity of the RimFortress player
/// </summary>
[NetworkedComponent, AutoGenerateComponentState, RegisterComponent]
public sealed partial class RimFortressPlayerComponent : Component
{
    /// <summary>
    /// Maps owned by a player
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public List<EntityUid> OwnedMaps = new();

    /// <summary>
    /// Pops owned by a player
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public List<EntityUid> Pops = new();

    [ViewVariables(VVAccess.ReadOnly)]
    public bool GotRoundstartPops;
}
