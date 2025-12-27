using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Conversation.Components;

/// <summary>
/// Used to store information about a conversation in which the entity is a participant.
/// </summary>
[RegisterComponent]
public sealed partial class ConversationActorComponent : Component
{
    /// <summary>
    /// Conversation script prototype
    /// </summary>
    [DataField, ViewVariables]
    public ProtoId<ConversationScriptPrototype> Script;

    /// <summary>
    /// All participants in the conversation with their identifiers
    /// </summary>
    [DataField, ViewVariables]
    public Dictionary<string, EntityUid> Actors = new();

    /// <summary>
    /// Identifier of the conversation participant for this entity
    /// </summary>
    [DataField, ViewVariables]
    public string ActorId;

    /// <summary>
    /// Next participant in the conversation
    /// </summary>
    [DataField, ViewVariables]
    public EntityUid NextActor = EntityUid.Invalid;

    /// <summary>
    /// The next line spoken by this entity in the conversation,
    /// if next in conversation
    /// </summary>
    [DataField, ViewVariables]
    public LocId? NextMessage;

    /// <summary>
    /// The coordinates where the conversation takes place
    /// </summary>
    [DataField, ViewVariables]
    public EntityCoordinates ConversationCoords;
}
