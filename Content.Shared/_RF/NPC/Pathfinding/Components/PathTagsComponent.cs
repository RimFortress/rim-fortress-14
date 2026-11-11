using Content.Shared._RF.NPC.Pathfinding.Prototypes;
using Content.Shared._RF.NPC.Pathfinding.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.NPC.Pathfinding.Components;

/// <summary>
/// A component that stores the current entities' path search tags.
/// </summary>
/// <remarks>
/// The tags are changed only through <see cref="SharedRfPathfindingSystem"/>, which keeps the navigation map up to date.
/// </remarks>
[RegisterComponent, Access(typeof(SharedRfPathfindingSystem))]
public sealed partial class PathTagsComponent : Component
{
    /// <summary>
    /// Tags that are always assigned to the entity. They are only read from the prototype: the ones that the systems add
    /// and take away while the game goes (like the kind of a door) are not saved, the systems give them again on startup.
    /// </summary>
    [DataField(readOnly: true)]
    public HashSet<ProtoId<PathTagPrototype>> Tags = new();
}
