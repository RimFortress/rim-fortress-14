using Robust.Shared.Serialization;

namespace Content.Shared._RF.Parallax.Fog;

public abstract class SharedFogOfWarSystem : EntitySystem
{

}

[Serializable, NetSerializable]
public sealed class FogOfWarChunkAdded(
    NetEntity grid,
    HashSet<Vector2i> modifiedTiles,
    Vector2i chunk) : EntityEventArgs
{
    public NetEntity Grid = grid;
    public HashSet<Vector2i> ModifiedTiles = modifiedTiles;
    public Vector2i Chunk = chunk;
}

[Serializable, NetSerializable]
public sealed class FogOfWarChunkRemoved(NetEntity grid, Vector2i chunk) : EntityEventArgs
{
    public NetEntity Grid = grid;
    public Vector2i Chunk = chunk;
}

[Serializable, NetSerializable]
public sealed class FogOfWarFullStateRequest(NetEntity requester, NetEntity grid) : EntityEventArgs
{
    public NetEntity Requester = requester;
    public NetEntity Grid = grid;
}

[Serializable, NetSerializable]
public sealed class FogOfWarFullStateMessage(NetEntity grid, HashSet<Vector2i> visitedChunks) : EntityEventArgs
{
    public NetEntity Grid = grid;
    public HashSet<Vector2i> VisitedChunks = visitedChunks;
}
