namespace Content.Shared._RF.Conversation.Components;

/// <summary>
/// Used to store information about a conversation in which the entity is a participant.
/// </summary>
[RegisterComponent]
public sealed partial class ConversationActorComponent : Component
{
    /// <summary>
    /// An entity that stores the current state of the conversation.
    /// </summary>
    /// <seealso cref="ConversationComponent"/>
    [ViewVariables]
    public EntityUid Conversation;
}
