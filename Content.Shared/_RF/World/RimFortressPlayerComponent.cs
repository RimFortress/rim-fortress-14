using Robust.Shared.GameStates;

namespace Content.Shared._RF.World;

/// <summary>
/// Represents the entity of the RimFortress player
/// </summary>
[NetworkedComponent, AutoGenerateComponentState, RegisterComponent]
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
    public TimeSpan NextEventTime { get; set; }

    [ViewVariables, AutoNetworkedField]
    public Color FactionColor { get; set; } = Color.Blue;
}
