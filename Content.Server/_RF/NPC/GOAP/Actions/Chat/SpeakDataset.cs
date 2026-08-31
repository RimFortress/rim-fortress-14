using Content.Server.Chat.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared.Chat;
using Content.Shared.Dataset;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._RF.NPC.GOAP.Actions.Chat;

/// <summary>
/// Forces the entity to speak a random string from the dataset
/// </summary>
public sealed partial class SpeakDataset : BaseGoapAction<SpeakDataset>
{
    /// <summary>
    /// Localized dataset with strings to speak
    /// </summary>
    [DataField(required: true)]
    public ProtoId<LocalizedDatasetPrototype> Dataset;

    /// <summary>
    /// Whether to hide message from chat window and logs.
    /// </summary>
    [DataField]
    public bool Hidden = true;
}

public sealed class SpeakDatasetGoapActionSystem : GoapActionSystem<SpeakDataset>
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ChatSystem _chat = default!;

    protected override bool ActionStartup(Entity<GoapComponent> ent, SpeakDataset action)
    {
        if (!_proto.Resolve(action.Dataset, out var proto))
        {
            ProtoNotFound(action.Dataset);
            return false;
        }

        var msg = Loc.GetString(_random.Pick(proto.Values));
        _chat.TrySendInGameICMessage(ent, msg, InGameICChatType.Speak, hideChat: action.Hidden, hideLog: action.Hidden);
        return true;
    }
}
