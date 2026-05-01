using Content.Shared._RF.NPC.GOAP;
using JetBrains.Annotations;
using Robust.Client.UserInterface.Controllers;

namespace Content.Client._RF.NPC.GOAP.UI;

public sealed class AiDevWindowUiController : UIController
{
    /// <summary>
    /// An event invoked when the client receives debug information about a GOAP NPC.
    /// </summary>
    public event Action<(EntityUid Target, GoapPlanDebugInfo? Plan, GoapStaticGraph Graph)>? OnDebugInfo;

    public event Action<GoapBreakpoint>? OnBreakpointAdded;
    public event Action<GoapBreakpoint>? OnBreakpointRemoved;
    public event Action<GoapBreakpoint>? OnBreakpointRaised;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<GoapDebugInfoMessage>(OnDebugInfoMessage);
        SubscribeNetworkEvent<GoapBreakpointMessage>(OnBreakpoint);
        SubscribeNetworkEvent<GoapBreakpointRemoveMessage>(OnRemoveBreakpoint);
    }

    private void OnDebugInfoMessage(GoapDebugInfoMessage msg, EntitySessionEventArgs args)
    {
        OnDebugInfo?.Invoke((EntityManager.GetEntity(msg.Target), msg.Plan, msg.Graph));

        if (msg.Breakpoint != null)
            OnBreakpointRaised?.Invoke(msg.Breakpoint.Value);
    }

    private void OnBreakpoint(GoapBreakpointMessage msg, EntitySessionEventArgs args)
    {
        OnBreakpointAdded?.Invoke(msg.Point);
    }

    private void OnRemoveBreakpoint(
        GoapBreakpointRemoveMessage msg,
        EntitySessionEventArgs args)
    {
        OnBreakpointRemoved?.Invoke(msg.Point);
    }

    /// <summary>
    /// Requests debug information about the GOAP NPC,
    /// which can be received by subscribing to <see cref="OnDebugInfo"/>.
    /// </summary>
    /// <param name="uid">GOAP NPC entity.</param>
    [PublicAPI]
    public void RequestDebug(EntityUid uid)
        => EntityManager
            .EntityNetManager
            .SendSystemNetworkMessage(new GoapDebugInfoRequest(EntityManager.GetNetEntity(uid)));

    [PublicAPI]
    public void AddBreakpoint(
        EntityUid target,
        int nodeId,
        int index,
        GoapBreakpointKind kind,
        GoapBreakpointResultKind result)
        => EntityManager
            .EntityNetManager
            .SendSystemNetworkMessage(new GoapBreakpointMessage(new(
                EntityManager.GetNetEntity(target),
                nodeId,
                index,
                kind,
                result)));

    [PublicAPI]
    public void AddBreakpoint(GoapBreakpoint breakpoint)
        => EntityManager
            .EntityNetManager
            .SendSystemNetworkMessage(new GoapBreakpointMessage(breakpoint));

    [PublicAPI]
    public void RemoveBreakpoint(GoapBreakpoint breakpoint)
        => EntityManager
            .EntityNetManager
            .SendSystemNetworkMessage(new GoapBreakpointRemoveMessage(breakpoint));
}
