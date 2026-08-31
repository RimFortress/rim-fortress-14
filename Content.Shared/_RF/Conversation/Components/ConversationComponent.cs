using Content.Shared.Chat;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Conversation.Components;

/// <summary>
/// A component that stores information about the current status of the conversation.
/// </summary>
[RegisterComponent]
public sealed partial class ConversationComponent : Component
{
    /// <summary>
    /// Conversation script prototype.
    /// </summary>
    [ViewVariables]
    public ProtoId<ConversationScriptPrototype> Script;

    /// <summary>
    /// Next participant in the conversation
    /// </summary>
    [ViewVariables]
    public HashSet<EntityUid> NextActors;

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
