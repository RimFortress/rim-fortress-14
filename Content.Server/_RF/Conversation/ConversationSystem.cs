using Content.Server._RF.NPC.Systems;
using Content.Server.Chat.Systems;
using Content.Shared._RF.Conversation;
using Content.Shared._RF.Conversation.Components;
using Content.Shared.Chat;

namespace Content.Server._RF.Conversation;

public sealed class ConversationSystem : SharedConversationSystem
{
    [Dependency] private readonly ChatSystem _chat = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ConversationActorComponent, NpcTaskGiven>(OnTaskGiven);
    }

    private void OnTaskGiven(EntityUid uid, ConversationActorComponent component, NpcTaskGiven args)
    {
        EndConversation(new(uid, component));
    }

    public bool SayLine(Entity<ConversationActorComponent?> ent, bool hidden = false)
    {
        if (!TryGetLine(ent, out var line))
            return false;

        _chat.TrySendInGameICMessage(ent, line, InGameICChatType.Speak, hideChat: hidden, hideLog: hidden);
        ContinueConversation(ent);
        return true;
    }
}
