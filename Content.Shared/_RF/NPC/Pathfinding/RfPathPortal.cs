using Robust.Shared.Map;

namespace Content.Shared._RF.NPC.Pathfinding;

/// <summary>
/// Connects 2 disparate locations, for example, 2 docking airlocks connecting 2 grids,
/// or an actual portal on the same grid.
/// </summary>
/// <remarks>Portals are two-way for now.</remarks>
/// <param name="Handle">The unique identifier of the portal.</param>
/// <param name="CoordinatesA">The first end of the portal.</param>
/// <param name="CoordinatesB">The second end of the portal.</param>
public readonly record struct RfPathPortal(int Handle, EntityCoordinates CoordinatesA, EntityCoordinates CoordinatesB)
{
    // Portals are identified by their handle only.
    public bool Equals(RfPathPortal other) => Handle == other.Handle;

    public override int GetHashCode() => Handle;
}
