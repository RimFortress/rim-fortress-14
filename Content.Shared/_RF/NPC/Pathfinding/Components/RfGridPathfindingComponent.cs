namespace Content.Shared._RF.NPC.Pathfinding.Components;

/// <summary>
/// Stores the navigation map of a grid.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class RfGridPathfindingComponent : Component
{
    /// <summary>
    /// Origins of the chunks that must be rebuilt.
    /// </summary>
    [ViewVariables]
    public readonly HashSet<Vector2i> DirtyChunks = new();

    /// <summary>
    /// The next time the map is allowed to update.
    /// </summary>
    [AutoPausedField]
    public TimeSpan NextUpdate;

    /// <summary>
    /// The chunks of the navigation map by their origins.
    /// </summary>
    [ViewVariables]
    public readonly Dictionary<Vector2i, RfPathfindingChunk> Chunks = new();

    /// <summary>
    /// The origin of the chunk where the portal is stored.
    /// </summary>
    [ViewVariables]
    public readonly Dictionary<RfPathPortal, Vector2i> PortalLookup = new();

    /// <summary>
    /// Portals whose neighbors must be updated.
    /// </summary>
    [ViewVariables]
    public readonly List<RfPathPortal> DirtyPortals = new();
}
