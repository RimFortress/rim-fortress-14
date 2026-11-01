namespace Content.Shared._RF.Trigger.Components.Triggers;

/// <summary>
/// Triggers the entity when someone joins a role of a situation (<c>Engagement</c>) that the entity owns, or leaves it, with the
/// keys <c>KIND.joined.ROLE</c> and <c>KIND.left.ROLE</c>, where the kind is the prototype of the situation. The user is the one
/// who joins or leaves. A situation belongs to an entity when the entity is among its owners, see <c>OwnershipSystem</c>.
/// </summary>
[RegisterComponent]
public sealed partial class TriggerOnEngagementComponent : Component;
