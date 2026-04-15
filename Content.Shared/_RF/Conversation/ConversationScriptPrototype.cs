using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;

namespace Content.Shared._RF.Conversation;

/// <summary>
/// Prototype conversation script between NPCs
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
    /// The actors in this conversation
    /// </summary>
    [DataField]
    public List<ConversationActorData> Actors = new();

    /// <summary>
    /// Effects that will be applied to conversation actors upon completion
    /// </summary>
    [DataField]
    public Dictionary<string, EntityEffect[]> Effects = new();

    /// <summary>
    /// List of conversation lines for each actor
    /// </summary>
    [DataField]
    public List<ConversationLine> Lines = new();
}

[DataDefinition]
public sealed partial class ConversationLine
{
    [DataField("id", required: true)]
    public string ActorId;

    [DataField("msg", required: true)]
    public LocId Message;
}

[DataDefinition]
public sealed partial class ConversationActorData
{
    /// <summary>
    /// Conversation actor identifier
    /// </summary>
    [DataField]
    public string Id = default!;

    /// <summary>
    /// Common requirements for the entity to take on this role
    /// </summary>
    /// <remarks>
    /// The null value will be passed to the actor parameter in these requirements
    /// </remarks>
    [DataField("reqs")]
    public List<ConversationActorRequirement> Requirements = new();

    /// <summary>
    /// Requirements for other participants in the conversation to take up this role
    /// </summary>
    [DataField("reqsFor")]
    public Dictionary<string, List<ConversationActorRequirement>> RequirementsFor = new();
}

[ImplicitDataDefinitionForInheritors]
public abstract partial class ConversationActorRequirement
{
    /// <summary>
    /// Should the check result be inverted
    /// </summary>
    [DataField]
    public bool Invert;

    /// <summary>
    /// Checks a potential participant in the conversation
    /// </summary>
    /// <param name="author">Entity on whose behalf the verification takes place</param>
    /// <param name="actor">Potential participant in the conversation</param>
    /// <param name="entMan">EntityManager</param>
    public abstract bool Check(EntityUid author, EntityUid? actor, EntityManager entMan);
}
