using Robust.Shared.GameStates;

namespace Content.Shared._RF.Parallax.Fog;

/// <summary>
/// A planet grid with this component has the mechanics of the fog of war
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(SharedFogOfWarSystem))]
public sealed partial class FogOfWarComponent : Component
{
    /// <summary>
    /// The entity of the fog of war grid
    /// </summary>
    [ViewVariables]
    public EntityUid FowGrid;

    /// <summary>
    /// Chunks covered in the fog of war
    /// </summary>
    [ViewVariables]
    public HashSet<Vector2i> FogChunks = new();

    /// <summary>
    /// Loaded chunks on which the fog of war has been dispelled
    /// </summary>
    [ViewVariables]
    public HashSet<Vector2i> ActiveChunks = new();

    /// <summary>
    /// Entities loaded in the fog of war
    /// </summary>
    [ViewVariables]
    public Dictionary<Vector2i, HashSet<EntityUid>> LoadedEntities = new();

    [ViewVariables]
    public Dictionary<Vector2i, HashSet<uint>> LoadedDecal = new();

    [DataField, ViewVariables, AutoNetworkedField]
    public Color FogColor = Color.DarkGray.WithAlpha(0.3f);
}
