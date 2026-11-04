using Content.Shared.Chat;
using Robust.Shared.Map;

namespace Content.Shared._RF.Conversation.Components;

/// <summary>
/// A kind of a situation (see <see cref="Content.Shared._RF.NPC.Engagement.Components.EngagementComponent"/>) that is a conversation
/// between NPCs: the order in which they speak, and the information about the current status of it. The lines are in the
/// localization by the id of the prototype of the entity: <c>conversation-id-line-N</c>.
/// </summary>
[RegisterComponent]
public sealed partial class ConversationComponent : Component
{
    /// <summary>
    /// Settings for the order in which actors speak lines.
    /// </summary>
    [DataField(required: true)]
    public ConversationOrderType Order = default!;

    /// <summary>
    /// Next participant in the conversation
    /// </summary>
    [ViewVariables]
    public HashSet<EntityUid> NextActors = new();

    /// <summary>
    /// The next line spoken by this entity in the conversation.
    /// </summary>
    [ViewVariables]
    public int NextMessage;

    /// <summary>
    /// The time when an actor will be able to say his line.
    /// </summary>
    [ViewVariables]
    public TimeSpan NextDelay;

    /// <summary>
    /// The type of line in the conversation that will be spoken.
    /// </summary>
    [ViewVariables]
    public InGameICChatType NextSpeakType;

    /// <summary>
    /// Should the next line of the conversation be spoken or skipped?
    /// </summary>
    [ViewVariables]
    public bool NextSpeak;

    /// <summary>
    /// The starting place for this conversation.
    /// </summary>
    [ViewVariables]
    public EntityCoordinates StartPosition;
}
