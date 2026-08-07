using Content.Shared._RF.Conversation.Systems;

namespace Content.Shared._RF.Conversation;

/// <summary>
/// A class that checks certain conditions for a potential conversation participant.
/// </summary>
[ImplicitDataDefinitionForInheritors]
public abstract partial class ConversationCondition
{
    /// <summary>
    /// Should the check result be inverted.
    /// </summary>
    [DataField]
    public bool Invert;

    /// <summary>
    /// Checks a potential participant in the conversation.
    /// </summary>
    /// <param name="author">Entity on whose behalf the verification takes place.</param>
    /// <param name="other">
    /// Potential participant in the conversation.
    /// Not null if in <see cref="ConversationActorData.RequirementsFor"/>.
    /// </param>
    /// <param name="checker">Condition checker.</param>
    public abstract bool Check(EntityUid author, EntityUid? other, IConversationConditionChecker checker);
}

public abstract partial class BaseConversationCondition<T> : ConversationCondition
    where T : BaseConversationCondition<T>
{
    public override bool Check(EntityUid author, EntityUid? other, IConversationConditionChecker checker)
    {
        if (this is not T type)
            return false;

        var result = checker.CheckCondition(author, other, type);
        return Invert ? !result : result;
    }
}

/// <summary>
/// An event triggered to check the conversation condition.
/// </summary>
/// <param name="Target">Entity on whose behalf the verification takes place.</param>
/// <param name="Other">Potential participant in the conversation.</param>
/// <param name="Condition">Condition.</param>
/// <param name="Result">Condition check result.</param>
/// <typeparam name="T">Condition type.</typeparam>
[ByRefEvent]
public record struct ConversationConditionCheckEvent<T>(
    EntityUid Target,
    EntityUid? Other,
    T Condition,
    bool Result)
    where T : BaseConversationCondition<T>;
