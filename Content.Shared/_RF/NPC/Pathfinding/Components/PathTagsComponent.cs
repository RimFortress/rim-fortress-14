using Content.Shared._RF.NPC.Pathfinding.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.NPC.Pathfinding.Components;

/// <summary>
/// A component that stores the current entities' path search tags.
/// </summary>
[RegisterComponent]
public sealed partial class PathTagsComponent : Component
{
    /// <summary>
    /// Tags that are always assigned to the entity.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<PathTagPrototype>> Tags = new();
}
