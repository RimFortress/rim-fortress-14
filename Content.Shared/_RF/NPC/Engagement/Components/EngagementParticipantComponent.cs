using Content.Shared._RF.NPC.Engagement.Prototypes;
using Content.Shared._RF.NPC.Engagement.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.NPC.Engagement.Components;

/// <summary>
/// A component that stores information about the agent's current engagement in a situations.
/// </summary>
[RegisterComponent]
[Access(typeof(EngagementSystem))]
public sealed partial class EngagementParticipantComponent : Component
{
    /// <summary>
    /// The situations in which an agent participates and the roles they perform.
    /// </summary>
    [DataField]
    public Dictionary<EntityUid, ProtoId<EngagementRolePrototype>> Membership = new();

    /// <summary>
    /// Invitations from an agent in other situations.
    /// </summary>
    [DataField]
    public HashSet<(EntityUid EngageUid, EntityUid Inviter, ProtoId<EngagementRolePrototype> Role)> Invites = new();
}
