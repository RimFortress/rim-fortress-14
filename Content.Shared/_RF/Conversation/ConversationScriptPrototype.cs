using System.Numerics;
using Content.Shared._RF.MathHelpers;
using Content.Shared.Chat;
using Content.Shared.EntityEffects;
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
    /// The actors in this conversation.
    /// </summary>
    [DataField]
    public List<ConversationActorData> Actors = new();

    /// <summary>
    /// Effects that will be applied to conversation actors upon completion.
    /// </summary>
    [DataField]
    public Dictionary<string, EntityEffect[]> Effects = new();

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

    [DataDefinition]
    public sealed partial class CustomOrderEntry
    {
        /// <summary>
        /// The ID of the actor who will perform this part of the conversation.
        /// </summary>
        [DataField]
        public string Id;

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
        public string? FaceTo;

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

[DataDefinition]
public sealed partial class ConversationActorData
{
    /// <summary>
    /// Conversation actor identifier.
    /// </summary>
    [DataField(required: true)]
    public string Id = string.Empty;

    /// <summary>
    /// Common requirements for the entity to take on this role.
    /// </summary>
    /// <remarks>
    /// The null value will be passed to the actor parameter in these requirements.
    /// </remarks>
    [DataField("reqs")]
    public List<ConversationCondition> Requirements = new();

    /// <summary>
    /// Requirements for other participants in the conversation to take up this role.
    /// </summary>
    [DataField("reqsFor")]
    public Dictionary<string, List<ConversationCondition>> RequirementsFor = new();
}
