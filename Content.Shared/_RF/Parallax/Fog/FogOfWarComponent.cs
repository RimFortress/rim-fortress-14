using Robust.Shared.GameStates;

namespace Content.Shared._RF.Parallax.Fog;

/// <summary>
/// A planet grid with this component has the mechanics of the fog of war
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(SharedFogOfWarSystem))]
public sealed partial class FogOfWarComponent : Component
{
    [DataField, ViewVariables, AutoNetworkedField]
    public bool Enabled { get; set; } = true;

    [ViewVariables]
    public List<Vector2i> LoadedChunks = new();
}
