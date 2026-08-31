using System.Numerics;
using Content.Shared._RF.MathHelpers;
using Content.Shared._RF.NPC.Engagement.Prototypes;
using Content.Shared.Chat;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;

namespace Content.Shared._RF.Conversation;

/// <summary>
/// Prototype conversation script between NPCs.
/// </summary>
[Prototype]
public sealed partial class ConversationScriptPrototype : IPrototype, IInheritingPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <inheritdoc />
    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<ConversationScriptPrototype>))]
    public string[]? Parents { get; private set; }

    /// <inheritdoc />
    [AbstractDataField, NeverPushInheritance]
    public bool Abstract { get; private set; }

    /// <summary>
    /// A situation that will be used to start a conversation.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<EngagementPrototype> Engagement;

    /// <summary>
    /// Settings for the order in which actors speak lines.
    /// </summary>
    [DataField(required: true)]
    public ConversationOrderType Order = default!;
}

[ImplicitDataDefinitionForInheritors]
public abstract partial class ConversationOrderType
{
    /// <summary>
    /// The minimum and maximum durations of the pause between lines.
    /// </summary>
    [DataField]
    public MinMaxFloat Delay = new(2.00f, 4.33f);
}

/// <summary>
/// A sequence in which lines are spoken one after another
/// by all the actors until the maximum number of lines is reached.
/// </summary>
public sealed partial class ConversationBasicOrderType : ConversationOrderType
{
    /// <summary>
    /// Maximum number of lines.
    /// </summary>
    [DataField(required: true)]
    public int Lines;

    /// <summary>
    /// The order in which the actors should say their lines.
    /// </summary>
    [DataField(required: true)]
    public List<ProtoId<EngagementRolePrototype>> Actors = new();

    /// <summary>
    /// The type of line in the conversation that will be spoken.
    /// </summary>
    [DataField]
    public InGameICChatType SpeakType = InGameICChatType.Speak;
}

/// <summary>
/// A sequence in which the actors speak their lines in a specified order.
/// </summary>
public sealed partial class ConversationCustomOrderType : ConversationOrderType
{
    /// <summary>
    /// A list of lines that specifies the custom order
    /// in which the actors will pronounce them and the pauses between them.
    /// </summary>
    [DataField(required: true)]
    public List<CustomOrderEntry> Custom = new();

    [DataRecord]
    public partial record struct CustomOrderEntry()
    {
        /// <summary>
        /// The ID of the actor who will perform this part of the conversation.
        /// </summary>
        [DataField(required: true)]
        public ProtoId<EngagementRolePrototype> Id = default!;

        /// <summary>
        /// If true, the actor will say his line; otherwise, he will perform the other necessary actions.
        /// </summary>
        [DataField]
        public bool Speak = true;

        /// <summary>
        /// The distance from the starting point of the conversation,
        /// where the actor should be standing when saying the line.
        /// </summary>
        [DataField]
        public Vector2? PosOffset;

        /// <summary>
        /// The direction the actor will face when saying the line.
        /// </summary>
        [DataField]
        public Vector2? FaceDir;

        /// <summary>
        /// The actor's ID, the face of whom the character will be facing when saying the line.
        /// </summary>
        [DataField]
        public ProtoId<EngagementRolePrototype>? FaceTo;

        /// <summary>
        /// The minimum and maximum durations of the pause after this.
        /// If null, the <see cref="ConversationCustomOrderType.Delay"/> value will be used.
        /// </summary>
        [DataField]
        public MinMaxFloat? Delay;

        /// <summary>
        /// The type of line in the conversation that will be spoken.
        /// </summary>
        [DataField]
        public InGameICChatType SpeakType = InGameICChatType.Speak;
    }
}
