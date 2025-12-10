using Content.Server.Chat.Systems;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared.Dataset;
using Content.Shared.Random.Helpers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._RF.NPC.HTN.Preconditions;

/// <summary>
/// Forces the entity to speak a random string from the dataset
/// </summary>
public sealed partial class SpeakDatasetOperator : HTNOperator
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    private ChatSystem _chat = default!;

    /// <summary>
    /// Localized dataset with strings to speak
    /// </summary>
    [DataField(required: true)]
    public ProtoId<LocalizedDatasetPrototype> Dataset;

    /// <summary>
    /// Whether to hide message from chat window and logs
    /// </summary>
    [DataField]
    public bool Hidden;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _chat = sysManager.GetEntitySystem<ChatSystem>();
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        var msg = Loc.GetString(_random.Pick(_prototype.Index(Dataset)));

        _chat.TrySendInGameICMessage(owner, msg, InGameICChatType.Speak, hideChat: Hidden, hideLog: Hidden);
        return HTNOperatorStatus.Finished;
    }
}
