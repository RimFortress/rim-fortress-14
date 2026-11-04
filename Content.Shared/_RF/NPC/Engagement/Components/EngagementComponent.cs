using Content.Shared._RF.NPC.Engagement.Prototypes;
using Content.Shared._RF.NPC.Engagement.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.NPC.Engagement.Components;

/// <summary>
/// A situation in which multiple AIs are involved in a single interaction: the entity that has it. The kinds of situations are
/// the prototypes of such entities (the kind of one is <c>Prototype(uid)</c>), so a situation can have any other components that a kind of it needs, such as the state
/// (<c>Goap</c>) in which it keeps what the participants have in common, and that they read as <c>Engagement/Kind/State/Key</c>.
/// </summary>
[RegisterComponent]
[Access(typeof(EngagementSystem))]
public sealed partial class EngagementComponent : Component
{
    /// <summary>
    /// A list of roles available in this situation.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<EngagementRolePrototype>> Roles = new();

    /// <summary>
    /// If true, the situation will be closed if the conditions regarding the number of roles are not met.
    /// </summary>
    [DataField]
    public bool DissolveInvalid = true;

    /// <summary>
    /// The amount of time given to potential participants in the situation to accept the invitation.
    /// </summary>
    [DataField]
    public TimeSpan InviteTime = TimeSpan.FromSeconds(7);

    /// <summary>
    /// A list of the participants in the situation and their roles.
    /// </summary>
    [ViewVariables]
    public Dictionary<ProtoId<EngagementRolePrototype>, HashSet<EntityUid>> Actors = new();

    /// <summary>
    /// Initiator of this situation.
    /// </summary>
    [ViewVariables]
    public EntityUid Initiator;

    /// <summary>
    /// True, if the situation has already begun;
    /// otherwise, waiting for a response to the invitations
    /// from the minimum number of participants required to get started.
    /// </summary>
    [ViewVariables]
    public bool Started;

    /// <summary>
    /// Invitations in this situation that have not yet been accepted.
    /// </summary>
    [ViewVariables]
    public readonly HashSet<(ProtoId<EngagementRolePrototype> Role, EntityUid Uid, TimeSpan ValidUntil)> Invites = new();

    /// <summary>
    /// Schedules the next <see cref="EngagementRolePrototype.AlwaysConditionCheck"/> re-evaluation.
    /// </summary>
    [ViewVariables]
    public readonly Dictionary<EntityUid, TimeSpan> NextConditionCheck = new();
}
