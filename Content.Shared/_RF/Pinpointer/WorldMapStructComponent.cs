namespace Content.Shared._RF.Pinpointer;

/// <summary>
/// An entity with this component will render on the world map
/// </summary>
[RegisterComponent]
public sealed partial class WorldMapStructComponent : Component
{
    /// <summary>
    /// Main color of rendering on the map
    /// </summary>
    [DataField(required: true)]
    public Color MainColor;

    /// <summary>
    /// Additional color used by some rendering methods
    /// </summary>
    [DataField]
    public Color SecondaryColor = Color.Black;

    /// <summary>
    /// Method of drawing the structure on the map
    /// </summary>
    [DataField]
    public WorldMapChunkType DrawType = WorldMapChunkType.Wall;
}

public enum WorldMapChunkType : byte
{
    Wall = 0,
    Tile = 1,
    Dot = 2,
}
