namespace Content.Shared._RF.NPC.Groups.Components;

/// <summary>
/// Marks a group (a situation, see <c>EngagementComponent</c>) that is being taken apart by <see cref="Systems.GroupsSystem"/>:
/// its participants are being moved somewhere else, or it is made again. The participants leave it, but that is not a loss for
/// those who watch the group, such as the owner's reactions to the leaving of a leader, so the triggers of leaving are not
/// raised for it.
/// </summary>
[RegisterComponent]
public sealed partial class GroupDissolvingComponent : Component;
