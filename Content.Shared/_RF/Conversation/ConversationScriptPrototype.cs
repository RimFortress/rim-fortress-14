using Content.Shared._RF.MathHelpers;
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
public abstract partial class ConversationOrderType;

/// <summary>
/// A sequence in which lines are spoken one after another
/// by all the actors until the maximum number of lines is reached.
/// </summary>
public sealed partial class ConversationSequentialOrderType : ConversationOrderType
{
    /// <summary>
    /// Maximum number of lines.
    /// </summary>
    [DataField(required: true)]
    public int Lines;

    /// <summary>
    /// The minimum and maximum durations of the pause between lines.
    /// </summary>
    [DataField]
    public MinMaxFloat Delay = new(1.33f, 3.33f);
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
    public List<CustomOrderLine> Custom = new();

    /// <param name="Id">The ID of the actor who will speak this line.</param>
    /// <param name="Delay">The minimum and maximum durations of the pause after this.</param>
    [Serializable]
    public readonly record struct CustomOrderLine(string Id, MinMaxFloat Delay);
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
