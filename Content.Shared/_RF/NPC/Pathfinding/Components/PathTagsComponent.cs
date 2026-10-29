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
    /// Tags that are always assigned to the entity.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<PathTagPrototype>> Tags = new();
}
