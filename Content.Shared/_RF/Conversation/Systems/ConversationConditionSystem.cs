namespace Content.Shared._RF.Conversation.Systems;

public abstract class ConversationConditionSystem<TComp, TCondition> : EntitySystem
    where TComp : Component
    where TCondition : BaseConversationCondition<TCondition>
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TComp, ConversationConditionCheckEvent<TCondition>>(OnConditionCheck);
    }

    private void OnConditionCheck(Entity<TComp> ent, ref ConversationConditionCheckEvent<TCondition> ev)
    {
        ev.Result = Check(ent, ev.Other, ev.Condition);
    }

    /// <summary>
    /// Checks a potential participant in the conversation.
    /// </summary>
    /// <param name="ent">Entity on whose behalf the verification takes place.</param>
    /// <param name="other">
    /// Potential participant in the conversation.
    /// Not null conversation from <see cref="ConversationActorData.RequirementsFor"/>.
    /// </param>
    /// <param name="condition">Condition.</param>
    protected abstract bool Check(Entity<TComp> ent, EntityUid? other, TCondition condition);
}

public abstract class ConversationConditionSystem<T> : EntitySystem where T : BaseConversationCondition<T>
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ConversationConditionCheckEvent<T>>(OnConditionCheck);
    }

    private void OnConditionCheck(ref ConversationConditionCheckEvent<T> ev)
    {
        ev.Result = Check(ev.Target, ev.Other, ev.Condition);
    }

    /// <summary>
    /// Checks a potential participant in the conversation.
    /// </summary>
    /// <param name="target">Entity on whose behalf the verification takes place.</param>
    /// <param name="other">
    /// Potential participant in the conversation.
    /// Not null conversation from <see cref="ConversationActorData.RequirementsFor"/>.
    /// </param>
    /// <param name="condition">Condition.</param>
    protected abstract bool Check(EntityUid target, EntityUid? other, T condition);
}
