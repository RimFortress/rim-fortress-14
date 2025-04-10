using System.Numerics;

namespace Content.Shared._RF.World;

/// <summary>
/// Represents the entity of the RimFortress world map
/// </summary>
[RegisterComponent]
public sealed partial class WorldMapComponent : Component
{
    /// <summary>
    /// The player who founded a settlement on this map
    /// </summary>
    [ViewVariables]
    public EntityUid? OwnerPlayer { get; set; }

    /// <summary>
    /// Map coordinates in the world grid
    /// </summary>
    [ViewVariables]
    public Vector2 WorldCoords { get; set; }

    [ViewVariables]
    public TimeSpan NextEventTime { get; set; }
}
