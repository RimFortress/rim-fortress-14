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

    [ViewVariables]
    public TimeSpan LastEventTime;

    [ViewVariables, AutoNetworkedField]
    public Color FactionColor = Color.Blue;
}
