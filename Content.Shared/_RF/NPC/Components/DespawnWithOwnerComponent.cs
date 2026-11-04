namespace Content.Shared._RF.NPC.Components;

/// <summary>
/// The entity lives as long as it is owned (see <c>OwnershipSystem</c>): when the last of its owners is gone or lets it go, it is
/// deleted. It is for the things that exist for the sake of someone and are nothing without them, such as the situations that an event
/// makes for the raiders (<c>Engagement</c>): when the event is over, there is nothing to clean up by hand.
/// An entity that never had an owner is not deleted, it is not about being owned, but about having lost the owner.
/// </summary>
[RegisterComponent]
public sealed partial class DespawnWithOwnerComponent : Component;
