using Content.Shared.NPC.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.World;

/// <summary>
/// Represents the entity of the RimFortress player
/// </summary>
[RegisterComponent]
public sealed partial class RimFortressPlayerComponent : Component
{
    [ViewVariables]
    public ProtoId<NpcFactionPrototype> Faction { get; set; }

    /// <summary>
    /// Maps owned by a player
    /// </summary>
    [ViewVariables]
    public List<EntityUid> OwnedMaps { get; set; } = new();

    /// <summary>
    /// Pops that can be controlled by the player
    /// </summary>
    [ViewVariables]
    public List<EntityUid> Pops { get; set; } = new();
}
