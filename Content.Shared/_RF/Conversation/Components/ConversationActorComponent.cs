using System.Numerics;
using Content.Shared._RF.NPC.GOAP;
using Robust.Shared.Map;

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

    /// <summary>
    /// Is the actor ready to start/continue the conversation?
    /// </summary>
    [ViewVariables]
    public bool Ready;

    /// <summary>
    /// Stores the position from which the next actor should speak the line.
    /// </summary>
    [ViewVariables]
    public EntityCoordinates TargetPos;

    /// <summary>
    /// The maximum radius from the target position within which the next actor can be located.
    /// </summary>
    [ViewVariables]
    public StateKey<float> TargetRangeKey;

    /// <summary>
    /// The coordinates in the direction the next actor should be facing.
    /// </summary>
    [ViewVariables]
    public Vector2 TargetFaceTo;

}
