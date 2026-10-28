using Content.Shared.Decals;
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

    /// <summary>
    /// Whether chunks that were visited by the player stay on the client as a frozen copy after they are unloaded
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool PersistChunks = true;

    /// <summary>
    /// Whether decals are copied to the frozen chunks.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool PersistDecals = true;

    [ViewVariables]
    public List<Vector2i> LoadedChunks = new();

    // Client-side cache of the frozen copy of the world

    /// <summary>
    /// Client-side grid that holds the frozen tiles and entities.
    /// </summary>
    [ViewVariables]
    public EntityUid FowGrid;

    /// <summary>
    /// Chunks that are currently frozen.
    /// </summary>
    [ViewVariables]
    public HashSet<Vector2i> FrozenChunks = new();

    [ViewVariables]
    public Dictionary<Vector2i, HashSet<EntityUid>> FrozenEntities = new();

    [ViewVariables]
    public Dictionary<Vector2i, HashSet<DecalIndex>> FrozenDecals = new();
}
