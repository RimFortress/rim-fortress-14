using System.Numerics;
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
    /// All participants in the conversation with their identifiers.
    /// </summary>
    [ViewVariables]
    public Dictionary<string, EntityUid> Actors = new();

    /// <summary>
    /// Next participant in the conversation
    /// </summary>
    [ViewVariables]
    public EntityUid NextActor;

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
    /// The coordinates in the direction the next actor should be facing.
    /// </summary>
    [ViewVariables]
    public Vector2 NextFaceTo;
}
