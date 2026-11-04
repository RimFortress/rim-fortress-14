using JetBrains.Annotations;

namespace Content.Shared._RF.NPC.Groups;

/// <summary>
/// Raised on a group (the situation entity) that has just been made by <see cref="Systems.GroupsSystem.Form"/>.
/// </summary>
/// <param name="Group">The new group.</param>
/// <param name="Owner">The owner of the group.</param>
[PublicAPI]
public readonly record struct GroupFormed(EntityUid Group, EntityUid Owner);

/// <summary>
/// Raised on the group that the participants of another group have been moved to by <see cref="Systems.GroupsSystem.Merge"/>.
/// </summary>
/// <param name="Group">The group that took the participants.</param>
/// <param name="From">The group that is gone, the entity is deleted at the time.</param>
[PublicAPI]
public readonly record struct GroupMerged(EntityUid Group, EntityUid From);
