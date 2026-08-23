using Content.Shared._RF.NPC.Engagement.Prototypes;
using Content.Shared._RF.NPC.Engagement.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.NPC.Engagement.Components;

/// <summary>
/// A component that stores data about the situation in which AIs is engaged.
/// </summary>
[RegisterComponent]
[Access(typeof(EngagementSystem))]
public sealed partial class EngagementComponent : Component
{
    /// <summary>
    /// Prototype of this situation.
    /// </summary>
    [DataField]
    public ProtoId<EngagementPrototype> Kind;

    /// <summary>
    /// A list of the participants in the situation and their roles.
    /// </summary>
    [DataField]
    public Dictionary<string, HashSet<EntityUid>> Actors = new();

    /// <summary>
    /// Initiator of this situation.
    /// </summary>
    [DataField]
    public EntityUid Initiator;

    /// <summary>
    /// True, if the situation has already begun;
    /// otherwise, waiting for a response to the invitations
    /// from the minimum number of participants required to get started.
    /// </summary>
    [DataField]
    public bool Started;

    /// <summary>
    /// Invitations in this situation that have not yet been accepted.
    /// </summary>
    [ViewVariables]
    public HashSet<(string Role, EntityUid Uid, TimeSpan ValidUntil)> Invites = new();

    /// <summary>
    /// Schedules the next <see cref="EngagementRole.AlwaysConditionCheck"/> re-evaluation.
    /// </summary>
    [ViewVariables]
    public readonly Dictionary<EntityUid, TimeSpan> NextConditionCheck = new();
}
