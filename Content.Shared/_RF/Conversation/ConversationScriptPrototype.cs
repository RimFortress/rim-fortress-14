using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Conversation;

/// <summary>
/// Prototype conversation script between NPCs
/// </summary>
[Prototype]
public sealed class ConversationScriptPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; } = default!;

    /// <summary>
    /// The actors in this dialogue
    /// </summary>
    [DataField]
    public List<ConversationActorData> Actors = new();

    /// <summary>
    /// Effects that will be applied to dialogue actors upon completion
    /// </summary>
    [DataField]
    public Dictionary<string, List<EntityEffect>> Effects = new();

    /// <summary>
    /// List of dialogue lines for each actor
    /// </summary>
    [DataField]
    public List<Dictionary<string, LocId>> Dialog = new();
}

[DataDefinition]
public sealed partial class ConversationActorData
{
    /// <summary>
    /// Dialogue actor identifier
    /// </summary>
    [DataField]
    public string Id = default!;

    /// <summary>
    /// Requirements for other participants in the dialogue to take up this role
    /// </summary>
    [DataField]
    public Dictionary<string, List<ConversationActorRequirement>> Requirements = new();
}

[ImplicitDataDefinitionForInheritors]
public abstract partial class ConversationActorRequirement
{
    /// <summary>
    /// Checks a potential participant in the conversation
    /// </summary>
    /// <param name="author">Entity on whose behalf the verification takes place</param>
    /// <param name="actor">Potential participant in the conversation</param>
    /// <param name="entMan">EntityManager</param>
    public abstract bool Check(EntityUid author, EntityUid actor, EntityManager entMan);
}
