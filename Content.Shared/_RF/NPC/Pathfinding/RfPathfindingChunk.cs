using JetBrains.Annotations;
using System.Runtime.InteropServices;
using Content.Shared._RF.NPC.Pathfinding.Components;
using Content.Shared._RF.NPC.Pathfinding.Systems;

namespace Content.Shared._RF.NPC.Pathfinding;

/// <summary>
/// A part of the navigation map of a grid: <see cref="SharedRfPathfindingSystem.ChunkSize"/> tiles on each side,
/// each tile consists of <see cref="SharedRfPathfindingSystem.SubStep"/> points on each side.
/// </summary>
/// <remarks>
/// Both points and tiles are stored in flat arrays, ordered by X first: <c>x * side + y</c>.
/// </remarks>
public sealed class RfPathfindingChunk
{
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
    /// The data of the points of the chunk, see <see cref="PointIndex"/>.
    /// </summary>
    [ViewVariables]
    public readonly RfPathfindingData[] Points = new RfPathfindingData[PointsPerSide * PointsPerSide];

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
        Array.Fill(Points, RfPathfindingData.Invalid);
    }

    /// <summary>
    /// The index in <see cref="Points"/> of the point with the given position in the chunk.
    /// </summary>
    [PublicAPI, Pure]
    public static int PointIndex(int x, int y) => x * PointsPerSide + y;

    /// <summary>
    /// The index in <see cref="Polys"/> of the tile with the given position in the chunk.
    /// </summary>
    [PublicAPI, Pure]
    public static int TileIndex(int x, int y) => x * SharedRfPathfindingSystem.ChunkSize + y;

    /// <summary>
    /// Returns the polys of the tile. The span is valid until the polys of the tile are changed.
    /// </summary>
    [PublicAPI, Pure]
    public ReadOnlySpan<RfPathPoly> GetPolys(int tile)
        => Polys[tile] is { } polys ? CollectionsMarshal.AsSpan(polys) : default;

    /// <summary>
    /// Returns the list of the tile's polys, creating it if needed.
    /// </summary>
    [PublicAPI]
    public List<RfPathPoly> EnsurePolys(int tile) => Polys[tile] ??= new List<RfPathPoly>(1);
}
