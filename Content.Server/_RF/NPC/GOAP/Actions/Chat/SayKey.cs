using Content.Server._RF.NPC.GOAP.Systems;
using Content.Server.Chat.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared.Chat;

namespace Content.Server._RF.NPC.GOAP.Actions.Chat;

/// <summary>
/// Makes the agent speak the contents of the key.
/// </summary>
public sealed partial class SayKey : BaseGoapAction<SayKey>
{
    [DataField(required: true)]
    public StateKey<object> Key = string.Empty;

    /// <summary>
    /// Whether to hide message from chat window and logs.
    /// </summary>
    [DataField]
    public bool Hidden;
}

public sealed class SayKeySystem : GoapActionSystem<SayKey>
{
    [Dependency] private readonly ChatSystem _chat = default!;

    protected override float ActionCost(Entity<GoapComponent> ent, GoapState state, SayKey action) => 0f;

    protected override bool ActionStartup(Entity<GoapComponent> ent, SayKey action)
    {
        if (!TryGetValue(ent, action, action.Key, out var value))
            return false;

        var @string = value.ToString();
        if (@string is not { })
        {
            CreateDump(ent, action, $"value of key '{action.Key}' is null");
            return false;
        }

        _chat.TrySendInGameICMessage(
            ent,
            @string,
            InGameICChatType.Speak,
            hideChat: action.Hidden,
            hideLog: action.Hidden);
        return true;
    }
}
