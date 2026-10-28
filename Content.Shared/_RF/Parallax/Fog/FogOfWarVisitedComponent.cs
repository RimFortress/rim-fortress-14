namespace Content.Shared._RF.Parallax.Fog;

/// <summary>
/// Stores the chunks of the fog of war maps that the owner (a player entity) has visited. These chunks stay frozen on the
/// client after they are unloaded. It is kept in a component, so it is saved with the game.
/// </summary>
[RegisterComponent, Access(typeof(SharedFogOfWarSystem))]
public sealed partial class FogOfWarVisitedComponent : Component
{
    /// <summary>
    /// Map with the <see cref="FogOfWarComponent"/> to the origins of the visited chunks that are not loaded right now
    /// </summary>
    [DataField]
    public Dictionary<EntityUid, HashSet<Vector2i>> Chunks = new();
}
