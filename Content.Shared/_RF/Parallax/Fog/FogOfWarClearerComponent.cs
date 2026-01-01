using Robust.Shared.GameStates;

namespace Content.Shared._RF.Parallax.Fog;

/// <summary>
/// This is used to dispel fog of war within a certain radius of the component's owner.
/// </summary>
[Access(typeof(SharedFogOfWarSystem))]
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FogOfWarClearerComponent : Component
{
    /// <summary>
    /// The radius at which the fog will dissipate
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Range = 14f;
}
