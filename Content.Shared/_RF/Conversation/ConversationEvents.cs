using Robust.Shared.Map;

namespace Content.Shared._RF.Conversation;

/// <summary>
/// Raised on a conversation when it is about to start, to find the place for it: where the participants are to meet,
/// and where each of them is to stand. The one that knows how to tell which places are fit
/// (the navigation map, which the shared part does not have) sets <see cref="Handled"/> and fills the rest;
/// if nobody does, the place is where the initiator is and everybody goes to it.
/// </summary>
[ByRefEvent]
public record struct ConversationPlaceEvent(EntityCoordinates Preferred, List<EntityUid> Actors)
{
    /// <summary>
    /// Whether the place has been found.
    /// </summary>
    public bool Handled;


    /// <summary>
    /// The place of the conversation.
    /// </summary>
    public EntityCoordinates Center;

    /// <summary>
    /// Where each participant stands, round <see cref="Center"/>. Those who are not in it go to the center.
    /// </summary>
    public readonly Dictionary<EntityUid, EntityCoordinates> Spots = new();
}
