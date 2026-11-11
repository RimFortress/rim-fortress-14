using JetBrains.Annotations;
using Content.Shared._RF.NPC.Pathfinding.Components;
using Content.Shared._RF.NPC.Pathfinding.Systems;

namespace Content.Shared._RF.NPC.Pathfinding;

/// <summary>
/// A path tag located on a tile.
/// </summary>
/// <param name="Tag">The index of the tag in the tag cache of <see cref="SharedRfPathfindingSystem"/>.</param>
/// <param name="Tile">The tile on the grid.</param>
public readonly record struct PathTagSource(byte Tag, Vector2i Tile);

/// <summary>
/// A part of the navigation map of a grid: <see cref="SharedRfPathfindingSystem.ChunkSize"/> tiles on each side,
/// each tile consists of <see cref="SharedRfPathfindingSystem.SubStep"/> points on each side.
/// </summary>
/// <remarks>
/// Only the result of the build is stored: the data of the points exists only while the chunk is being built,
/// so a chunk takes little memory. Tiles are stored in a flat array, ordered by X first: <c>x * side + y</c>.
/// </remarks>
public sealed class RfPathfindingChunk
{
    private static readonly RfPathPoly[] NoPolys = Array.Empty<RfPathPoly>();

    /// <summary>
    /// The number of points on each side of the chunk.
    /// </summary>
    public const int PointsPerSide = SharedRfPathfindingSystem.ChunkSize * SharedRfPathfindingSystem.SubStep;

    /// <summary>
    /// The number of tiles in the chunk.
    /// </summary>
    public const int TileCount = SharedRfPathfindingSystem.ChunkSize * SharedRfPathfindingSystem.ChunkSize;

    /// <summary>
    /// The origin of the chunk on the grid.
    /// </summary>
    [ViewVariables]
    public readonly Vector2i Origin;

    /// <summary>
    /// The last time a search or a rebuild used the chunk, see <see cref="RfGridPathfindingComponent.ChunkLifetime"/>.
    /// </summary>
    [ViewVariables]
    public TimeSpan LastUsed;

    /// <summary>
    /// The path tags located on the tiles of the chunk, they influence this and the neighboring chunks.
    /// </summary>
    [ViewVariables]
    public PathTagSource[] Sources = Array.Empty<PathTagSource>();

    /// <summary>
    /// The polys of each tile of the chunk, see <see cref="TileIndex"/>. A tile without polys has no list.
    /// </summary>
    [ViewVariables]
    public readonly List<RfPathPoly>?[] Polys = new List<RfPathPoly>?[TileCount];

    /// <summary>
    /// The polys that this chunk's portals lead from.
    /// </summary>
    [ViewVariables]
    public readonly Dictionary<RfPathPortal, RfPathPoly> PortalPolys = new();

    /// <summary>
    /// This chunk's portals.
    /// </summary>
    [ViewVariables]
    public readonly List<RfPathPortal> Portals = new();

    /// <summary>
    /// Creates an empty chunk whose points are not calculated yet.
    /// </summary>
    /// <param name="origin">The origin of the chunk on the grid.</param>
    public RfPathfindingChunk(Vector2i origin)
    {
        Origin = origin;
    }

    /// <summary>
    /// The index of the point with the given position in the chunk, in the buffer the points are built in.
    /// </summary>
    [PublicAPI, Pure]
    public static int PointIndex(int x, int y) => x * PointsPerSide + y;

    /// <summary>
    /// The index in <see cref="Polys"/> of the tile with the given position in the chunk.
    /// </summary>
    [PublicAPI, Pure]
    public static int TileIndex(int x, int y) => x * SharedRfPathfindingSystem.ChunkSize + y;

    /// <summary>
    /// Returns the polys of the tile, an empty list if it has none.
    /// </summary>
    /// <remarks>
    /// Go through the list with an index: the enumerator of an interface allocates.
    /// </remarks>
    [PublicAPI, Pure]
    public IReadOnlyList<RfPathPoly> GetPolys(int tile) => (IReadOnlyList<RfPathPoly>?) Polys[tile] ?? NoPolys;

    /// <summary>
    /// Returns the list of the tile's polys, creating it if needed.
    /// </summary>
    [PublicAPI]
    public List<RfPathPoly> EnsurePolys(int tile) => Polys[tile] ??= new List<RfPathPoly>(1);
}
