using System.Threading;
using Content.Shared._RF.NPC.Pathfinding.Prototypes;
using Robust.Shared.Map;
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

    /// <summary>
    /// The state of the path request of the agent.
    /// </summary>
    [ViewVariables]
    public PathRequestStatus RequestStatus;

    /// <summary>
    /// The profiles the request is going through, in the order they are tried: the next one is tried when the
    /// search with the previous one did not find a path.
    /// </summary>
    [ViewVariables]
    public List<ProtoId<PathProfilePrototype>> RequestProfiles = new();

    /// <summary>
    /// The index in <see cref="RequestProfiles"/> of the profile the search is going with.
    /// </summary>
    [ViewVariables]
    public int RequestProfileIndex;

    /// <summary>
    /// Where the request is to lead, null if it is not to lead anywhere in particular.
    /// </summary>
    [ViewVariables]
    public EntityCoordinates? RequestTarget;

    /// <summary>
    /// How close to the target the agent needs to get.
    /// </summary>
    [ViewVariables]
    public float RequestRange;

    /// <summary>
    /// The region the target takes up, if the range is counted from it and not from the point.
    /// </summary>
    [ViewVariables]
    public Box2? RequestTargetBounds;

    /// <summary>
    /// The job that is searching for the path.
    /// </summary>
    [ViewVariables]
    public PathJob? Job;

    /// <summary>
    /// Cancels the <see cref="Job"/>.
    /// </summary>
    public CancellationTokenSource? JobCancel;

    /// <summary>
    /// The result of the request: the path when it is found, or the best of what the profiles found.
    /// </summary>
    [ViewVariables]
    public PathSearchResult RequestResult = PathSearchResult.NoPath;

    /// <summary>
    /// The profile that gave the <see cref="RequestResult"/>.
    /// </summary>
    [ViewVariables]
    public ProtoId<PathProfilePrototype>? RequestProfile;
}
