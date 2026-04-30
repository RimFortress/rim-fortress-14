using Content.Shared._RF.NPC.GOAP;
using Robust.Client.UserInterface.Controllers;

namespace Content.Client._RF.NPC.GOAP.UI;

public sealed class AiDevWindowUiController : UIController
{
    /// <summary>
    /// An event invoked when the client receives debug information about a GOAP NPC.
    /// </summary>
    public event Action<(EntityUid Target, GoapPlanDebugInfo? Plan, GoapStaticGraph Graph)>? OnDebugInfo;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<GoapDebugInfoMessage>(OnDebugInfoMessage);
    }

    private void OnDebugInfoMessage(GoapDebugInfoMessage msg, EntitySessionEventArgs args)
    {
        OnDebugInfo?.Invoke((EntityManager.GetEntity(msg.Target), msg.Plan, msg.Graph));
    }

    /// <summary>
    /// Requests debug information about the GOAP NPC,
    /// which can be received by subscribing to <see cref="OnDebugInfo"/>.
    /// </summary>
    /// <param name="uid">GOAP NPC entity.</param>
    public void RequestDebug(EntityUid uid)
        => EntityManager
            .EntityNetManager
            .SendSystemNetworkMessage(new GoapDebugInfoRequest(EntityManager.GetNetEntity(uid)));
}
