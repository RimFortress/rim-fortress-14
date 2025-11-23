using Content.Server.Chat.Systems;
using Content.Shared._RF.Conversation;

namespace Content.Server._RF.Dialog;

public sealed class ConversationSystem : SharedConversationSystem
{
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

        if (ev.Message == line)
            ContinueConversation(script.Value, ev.Source);
        else
            EndConversation(ev.Source);
    }
}
