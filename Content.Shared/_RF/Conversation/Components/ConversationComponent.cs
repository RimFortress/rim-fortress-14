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
    /// How the place of the conversation is looked for, and the spots of the participants at it.
    /// </summary>
    [DataField]
    public ConversationPlaceSettings Place = new();

    /// <summary>
    /// The starting place for this conversation.
    /// </summary>
    [ViewVariables]
    public EntityCoordinates StartPosition;
}

/// <summary>
/// How the place of a conversation is found by the navigation map (see <see cref="ConversationPlaceEvent"/>): where the participants can
/// stand, and the spots round the place that they are given so that they do not stand in a crowd.
/// </summary>
[DataDefinition]
public sealed partial class ConversationPlaceSettings
{
    /// <summary>
    /// How far from the middle of the participants the place is looked for, in meters.
    /// </summary>
    [DataField]
    public float SearchRadius = 6f;

    /// <summary>
    /// How many nodes of the navigation map are looked at for the place at most.
    /// </summary>
    [DataField]
    public int SearchNodes = 400;

    /// <summary>
    /// How far from the middle of the participants the nodes are counted to find out whether the place is joined to each of them, by what
    /// they go through (not by a wall or a pit), in meters.
    /// </summary>
    [DataField]
    public float ReachRadius = 35f;

    /// <summary>
    /// How many nodes are looked at for that at most.
    /// </summary>
    [DataField]
    public int ReachNodes = 2500;

    /// <summary>
    /// How close to such a node a participant has to be, in meters, to be counted as joined to the place.
    /// </summary>
    [DataField]
    public float ReachMargin = 1.5f;

    /// <summary>
    /// How far from the place the spot of a participant is at least, in meters.
    /// </summary>
    [DataField]
    public float MinSpotRadius = 0.8f;

    /// <summary>
    /// How much farther for each participant: the more they are, the wider the circle of the spots.
    /// </summary>
    [DataField]
    public float SpotRadiusPerActor = 0.35f;

    /// <summary>
    /// How far from the radius of the circle the node of a spot can be, in meters: the nodes are tiles, and the circle is made of what there is.
    /// </summary>
    [DataField]
    public float SpotBand = 0.45f;

    /// <summary>
    /// The least distance between two spots, in meters.
    /// </summary>
    [DataField]
    public float MinSeparation = 0.9f;

    /// <summary>
    /// How many nodes are tried as the place of the conversation, the closest to the middle of the participants first.
    /// </summary>
    [DataField]
    public int MaxCenters = 30;
}
