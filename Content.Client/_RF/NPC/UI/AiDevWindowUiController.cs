using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.UtilityAi;
using JetBrains.Annotations;
using Robust.Client.UserInterface.Controllers;

namespace Content.Client._RF.NPC.UI;

public sealed class AiDevWindowUiController : UIController
{
    /// <summary>
    /// An event invoked when the client receives debug information about a GOAP NPC.
    /// </summary>
    public event Action<(EntityUid Target, GoapPlanDebugInfo? Plan, GoapStaticGraphDebug Graph)>? OnGoapDebugInfo;

    /// <summary>
    /// An event invoked when the client receives debug information about a Utility AI NPC.
    /// </summary>
    public event Action<(EntityUid Target, UtilityAiDebugInfo Info)>? OnUtilityAiDebugInfo;

    public event Action<GoapBreakpoint>? OnBreakpointAdded;
    public event Action<GoapBreakpoint>? OnBreakpointRemoved;

    public event Action<(GoapBreakpoint Point, GoapPlanDebugInfo Plan)>? OnBreakpointHit;

    public List<GoapBreakpoint> GoapBreakpoints { get; private set; } = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<GoapDebugInfoMessage>(OnGoapDebugInfoMessage);
        SubscribeNetworkEvent<UtilityAiDebugInfoMessage>(OnUtilityAiDebugInfoMessage);
        SubscribeNetworkEvent<GoapBreakpointMessage>(OnBreakpoint);
        SubscribeNetworkEvent<GoapBreakpointRemoveMessage>(OnRemoveBreakpoint);
        SubscribeNetworkEvent<GoapBreakpointHitMessage>(OnBreakpointHitMessage);
    }

    private void OnGoapDebugInfoMessage(GoapDebugInfoMessage msg, EntitySessionEventArgs args)
    {
        OnGoapDebugInfo?.Invoke((EntityManager.GetEntity(msg.Target), msg.Plan, msg.GraphDebug));
    }

    private void OnUtilityAiDebugInfoMessage(UtilityAiDebugInfoMessage msg, EntitySessionEventArgs args)
    {
        OnUtilityAiDebugInfo?.Invoke((EntityManager.GetEntity(msg.Target), msg.Info));
    }

    private void OnBreakpoint(GoapBreakpointMessage msg, EntitySessionEventArgs args)
    {
        GoapBreakpoints.Add(msg.Point);
        OnBreakpointAdded?.Invoke(msg.Point);
    }

    private void OnRemoveBreakpoint(GoapBreakpointRemoveMessage msg, EntitySessionEventArgs args)
    {
        GoapBreakpoints.Remove(msg.Point);
        OnBreakpointRemoved?.Invoke(msg.Point);
    }

    private void OnBreakpointHitMessage(GoapBreakpointHitMessage msg, EntitySessionEventArgs args)
    {
        GoapBreakpoints.Remove(msg.Point);
        OnBreakpointHit?.Invoke((msg.Point, msg.Plan));
    }

    /// <summary>
    /// Requests debug information about the GOAP NPC,
    /// which can be received by subscribing to <see cref="OnGoapDebugInfo"/>.
    /// </summary>
    /// <param name="uid">GOAP NPC entity.</param>
    [PublicAPI]
    public void RequestGoapDebug(EntityUid uid)
        => EntityManager
            .EntityNetManager
            .SendSystemNetworkMessage(new GoapDebugInfoRequest(EntityManager.GetNetEntity(uid)));

    /// <summary>
    /// Requests debug information about the Utility AI NPC,
    /// which can be received by subscribing to <see cref="OnUtilityAiDebugInfo"/>.
    /// </summary>
    /// <param name="uid">Utility AI NPC entity.</param>
    [PublicAPI]
    public void RequestUtilityAiDebug(EntityUid uid)
        => EntityManager
            .EntityNetManager
            .SendSystemNetworkMessage(new UtilityAiDebugInfoRequest(EntityManager.GetNetEntity(uid)));

    [PublicAPI]
    public void AddBreakpoint(
        EntityUid target,
        int nodeId,
        int index,
        GoapBreakpointKind kind,
        GoapBreakpointResultKind result)
        => AddBreakpoint(new(
            EntityManager.GetNetEntity(target),
            nodeId,
            kind == GoapBreakpointKind.Planning ? -1 : index,
            kind,
            result));

    [PublicAPI]
    public void AddBreakpoint(GoapBreakpoint breakpoint)
        => EntityManager
            .EntityNetManager
            .SendSystemNetworkMessage(new GoapBreakpointMessage(breakpoint));

    [PublicAPI]
    public void RemoveBreakpoint(
        EntityUid target,
        int nodeId,
        int index,
        GoapBreakpointKind kind,
        GoapBreakpointResultKind result)
        => RemoveBreakpoint(new(
            EntityManager.GetNetEntity(target),
            nodeId,
            index,
            kind,
            result));

    [PublicAPI]
    public void RemoveBreakpoint(GoapBreakpoint breakpoint)
        => EntityManager
            .EntityNetManager
            .SendSystemNetworkMessage(new GoapBreakpointRemoveMessage(breakpoint));

    [PublicAPI]
    public void Subscribe(EntityUid target)
        => EntityManager
            .EntityNetManager
            .SendSystemNetworkMessage(new GoapDebugInfoSubscriptionMessage(
                EntityManager.GetNetEntity(target),
                true));

    [PublicAPI]
    public void Unsubscribe(EntityUid target)
        => EntityManager
            .EntityNetManager
            .SendSystemNetworkMessage(new GoapDebugInfoSubscriptionMessage(
                EntityManager.GetNetEntity(target),
                false));
}
