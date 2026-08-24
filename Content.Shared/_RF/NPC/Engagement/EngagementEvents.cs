using Content.Shared._RF.NPC.Engagement.Prototypes;
using Content.Shared._RF.NPC.Engagement.Systems;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.NPC.Engagement;

/// <summary>
/// Raised on the situation entity and on every current participant once every role has
/// reached its <see cref="EngagementRole.MinCount"/> for the first time.
/// </summary>
/// <param name="Engagement">The situation entity.</param>
/// <param name="Kind">The situation's prototype.</param>
[PublicAPI]
public readonly record struct EngagementStarted(
    EntityUid Engagement,
    ProtoId<EngagementPrototype> Kind);

/// <summary>
/// Raised on the joining actor and on the situation entity when a role is occupied,
/// whether by direct force-join or by an accepted invite.
/// </summary>
/// <param name="Engagement">The situation entity.</param>
/// <param name="Actor">The entity that joined.</param>
/// <param name="Role">The role it now occupies.</param>
[PublicAPI]
public readonly record struct EngagementRoleJoined(
    EntityUid Engagement,
    EntityUid Actor,
    string Role);

/// <summary>
/// Raised on the leaving actor and on the situation entity when a participant leaves a role,
/// whether voluntarily, by dissolution, or by interruption.
/// </summary>
/// <param name="Engagement">The situation entity.</param>
/// <param name="Actor">The entity that left.</param>
/// <param name="Role">The role it no longer occupies.</param>
/// <param name="Reason">Why it left.</param>
[PublicAPI]
public readonly record struct EngagementRoleLeft(
    EntityUid Engagement,
    EntityUid Actor,
    string Role,
    EngagementEndReason Reason);

/// <summary>
/// Raised on the situation entity when it is fully dissolved for every remaining participant.
/// </summary>
/// <param name="Engagement">The situation entity.</param>
/// <param name="Reason">Why the situation ended.</param>
[PublicAPI]
public readonly record struct EngagementEnded(
    EntityUid Engagement,
    EngagementEndReason Reason);

/// <summary>
/// Raised on the invited and inviter entity and on the situation
/// entity when an invite is sent for a non-forced role.
/// </summary>
/// <param name="Engagement">The situation entity.</param>
/// <param name="Inviter">The inviter entity.</param>
/// <param name="Invited">The invited entity.</param>
/// <param name="Role">The role being offered.</param>
[PublicAPI]
public readonly record struct EngagementInviteSent(
    EntityUid Engagement,
    EntityUid Inviter,
    EntityUid Invited,
    string Role);

/// <summary>
/// Raised on the invited and inviter entity and on the situation entity when
/// a pending invite is withdrawn, whether by explicit cancellation or by expiring.
/// </summary>
/// <param name="Engagement">The situation entity.</param>
/// <param name="Inviter">The inviter entity.</param>
/// <param name="Invited">The entity whose invite was removed.</param>
[PublicAPI]
public readonly record struct EngagementInviteRemoved(
    EntityUid Engagement,
    EntityUid Inviter,
    EntityUid Invited);
