using Robust.Shared.GameStates;

namespace Content.Shared._RF.World.Components;

/// <summary>
/// Represents the entity of the RimFortress player
/// </summary>
[NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true), RegisterComponent]
public sealed partial class RimFortressPlayerComponent : Component
{
    /// <summary>
    /// Pops owned by a player
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public List<EntityUid> Pops = new();

    [ViewVariables(VVAccess.ReadOnly)]
    public bool GotRoundstartPops;

    /// <summary>
    /// The player has lost all of the pops that were alive and has been thrown out into the ghosts, see the defeat system.
    /// </summary>
    [ViewVariables]
    public bool Defeated;

    [ViewVariables, AutoNetworkedField]
    public Color FactionColor = Color.Blue;
}
