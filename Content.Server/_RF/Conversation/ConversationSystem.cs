using Content.Server.Chat.Systems;
using Content.Shared._RF.Conversation;

namespace Content.Server._RF.Conversation;

public sealed class ConversationSystem : SharedConversationSystem
{
    [Dependency] private readonly ChatSystem _chat = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EntitySpokeEvent>(OnSpeak);
    }

    private void OnSpeak(EntitySpokeEvent ev)
    {
        if (!TryGetScript(ev.Source, out var script)
            || !TryGetLine(script.Value, ev.Source, out var line))
            return;

        if (ev.Message == _chat.TransformSpeech(ev.Source, line.Trim()))
            ContinueConversation(script.Value, ev.Source);
        else
            EndConversation(ev.Source);
    }
}
