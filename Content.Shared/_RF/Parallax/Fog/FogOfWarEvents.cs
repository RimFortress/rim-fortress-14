using Robust.Shared.Serialization;

namespace Content.Shared._RF.Parallax.Fog;

/// <summary>
/// The chunk was unloaded on the server, the client should keep a frozen copy of it.
/// </summary>
/// <param name="map">Map with the <see cref="FogOfWarComponent"/></param>
/// <param name="chunk">Chunk origin in tiles</param>
/// <param name="modifiedTiles">Tiles in the chunk changed by players</param>
[Serializable, NetSerializable]
public sealed class FogOfWarChunkFrozen(NetEntity map, Vector2i chunk, HashSet<Vector2i> modifiedTiles) : EntityEventArgs
{
    public NetEntity Map = map;
    public Vector2i Chunk = chunk;
    public HashSet<Vector2i> ModifiedTiles = modifiedTiles;
}

/// <summary>
/// The chunk was loaded on the server, the frozen copy should be removed.
/// </summary>
[Serializable, NetSerializable]
public sealed class FogOfWarChunkUnfrozen(NetEntity map, Vector2i chunk) : EntityEventArgs
{
    public NetEntity Map = map;
    public Vector2i Chunk = chunk;
}

[Serializable, NetSerializable]
public sealed class FogOfWarFullStateRequest(NetEntity map) : EntityEventArgs
{
    public NetEntity Map = map;
}

/// <summary>
/// All chunks that the player has visited and that are not loaded right now.
/// </summary>
/// <param name="chunks">Chunk origin in tiles to modified tiles in this chunk</param>
[Serializable, NetSerializable]
public sealed class FogOfWarFullStateMessage(NetEntity map, Dictionary<Vector2i, HashSet<Vector2i>> chunks) : EntityEventArgs
{
    public NetEntity Map = map;
    public Dictionary<Vector2i, HashSet<Vector2i>> Chunks = chunks;
}
