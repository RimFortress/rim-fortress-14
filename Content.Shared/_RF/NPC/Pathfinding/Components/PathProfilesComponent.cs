using Content.Shared._RF.NPC.Pathfinding.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.NPC.Pathfinding.Components;

/// <summary>
/// A component that stores the path search profiles available for use by the agent
/// and the state of its current path request.
/// </summary>
[RegisterComponent]
public sealed partial class PathProfilesComponent : Component
{
    /// <summary>
    /// When searching for a path, these profiles will be checked
    /// one by one until the first available profile is found.
    /// </summary>
    [DataField(required: true)]
    public List<ProtoId<PathProfilePrototype>> Profiles = new();

    /// <summary>
    /// The profile of the path request that is in progress.
    /// </summary>
    [ViewVariables]
    public ProtoId<PathProfilePrototype>? ActiveProfile;

    /// <summary>
    /// Rules of the <see cref="ActiveProfile"/> whose agent conditions are met.
    /// </summary>
    [ViewVariables]
    public List<PathfindingRule> ActiveRules = new();

    /// <summary>
    /// All the ways the agent is able to deal damage during the path request.
    /// </summary>
    [ViewVariables]
    public List<PathAttack> Attacks = new();
}
