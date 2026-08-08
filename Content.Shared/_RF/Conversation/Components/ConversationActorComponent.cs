using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Conversation.Components;

/// <summary>
/// Used to store information about a conversation in which the entity is a participant.
/// </summary>
[RegisterComponent]
public sealed partial class ConversationActorComponent : Component
{
    /// <summary>
    /// Conversation script prototype.
    /// </summary>
    [ViewVariables]
    public ProtoId<ConversationScriptPrototype> Script;

    /// <summary>
    /// All participants in the conversation with their identifiers.
    /// </summary>
    [ViewVariables]
    public Dictionary<string, EntityUid> Actors = new();

    /// <summary>
    /// Identifier of the conversation participant for this entity.
    /// </summary>
    [ViewVariables]
    public string ActorId;

    /// <summary>
    /// Next participant in the conversation
    /// </summary>
    [ViewVariables]
    public EntityUid NextActor = EntityUid.Invalid;

    /// <summary>
    /// The next line spoken by this entity in the conversation.
    /// </summary>
    [ViewVariables]
    public int NextMessage;

    /// <summary>
    /// The time when an actor will be able to say his line.
    /// </summary>
    [DataField]
    public TimeSpan NextDelay = TimeSpan.Zero;
}
