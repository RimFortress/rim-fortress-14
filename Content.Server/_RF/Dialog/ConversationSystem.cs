using Content.Server._RF.NPC.Systems;
using Content.Server.Chat.Systems;
using Content.Server.NPC.HTN;
using Content.Shared._RF.Conversation;

namespace Content.Server._RF.Dialog;

public sealed class ConversationSystem : SharedConversationSystem
{
    [Dependency] private readonly ChatSystem _chat = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EntitySpokeEvent>(OnSpeak);
        SubscribeLocalEvent<HTNComponent, NpcTaskFinished>(OnTaskFinished);
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

    // Helps to complete the dialogue for all participants if it was interrupted for any reason
    private void OnTaskFinished(EntityUid uid, HTNComponent component, NpcTaskFinished ev)
    {
        EndConversation(uid);
    }
}
