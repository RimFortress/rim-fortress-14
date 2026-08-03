using System.Linq;
using Content.Shared._RF.NPC;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared.Administration;
using JetBrains.Annotations;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server._RF.NPC.GOAP.Systems;

public partial class GoapSystem
{
    private void InitializeDebug()
    {
        SubscribeNetworkEvent<GoapDebugInfoRequest>(OnDebugInfoRequest);
        SubscribeNetworkEvent<GoapDebugInfoSubscriptionMessage>(OnGoapDebugInfoSubscriptionMessage);
        SubscribeNetworkEvent<GoapBreakpointMessage>(OnBreakpoint);
        SubscribeNetworkEvent<GoapBreakpointRemoveMessage>(OnRemoveBreakpoint);
    }

    private void OnDebugInfoRequest(GoapDebugInfoRequest request, EntitySessionEventArgs args)
    {
        if (_admin.HasAdminFlag(args.SenderSession, AdminFlags.Debug)
            && TryGetEntity(request.Target, out var target)
            && HasComp<GoapComponent>(target))
            QueueDebugSend(args.SenderSession, target.Value);
    }

    private void OnGoapDebugInfoSubscriptionMessage(GoapDebugInfoSubscriptionMessage msg, EntitySessionEventArgs args)
    {
        if (!_admin.HasAdminFlag(args.SenderSession, AdminFlags.Debug)
            || !TryGetEntity(msg.Target, out var target)
            || !HasComp<GoapComponent>(target))
            return;

        if (!msg.Subscription)
        {
            if (DebugSubscriptions.TryGetValue(target.Value, out var sessions))
                sessions.Remove(args.SenderSession);

            return;
        }

        if (!DebugSubscriptions.GetOrNew(target.Value).Add(args.SenderSession))
            return;

        QueueDebugSend(args.SenderSession, target.Value);
    }

    private void OnBreakpoint(GoapBreakpointMessage msg, EntitySessionEventArgs args)
    {
        if (!_admin.HasAdminFlag(args.SenderSession, AdminFlags.Debug)
            || !TryGetEntity(msg.Point.Target, out var target)
            || !HasComp<GoapComponent>(target))
            return;

        // Holy shit
        if (msg.Point.Kind is GoapBreakpointKind.Planning or GoapBreakpointKind.ActionStartup
            && msg.Point.Result != GoapBreakpointResultKind.True
            && msg.Point.Result != GoapBreakpointResultKind.False
            || msg.Point.Kind == GoapBreakpointKind.ActionUpdate
            && msg.Point.Result != GoapBreakpointResultKind.Continuing
            && msg.Point.Result != GoapBreakpointResultKind.Failed
            && msg.Point.Result != GoapBreakpointResultKind.Finished
            || msg.Point.Kind is GoapBreakpointKind.ActionShutdown or GoapBreakpointKind.ActionPlanShutdown
            && msg.Point.Result != GoapBreakpointResultKind.None)
        {
            DebugTools.Assert(false, $"wrong goap breakpoint settings, kind: {msg.Point.Kind}, result: {msg.Point.Result}");
            return;
        }

        if (Breakpoints.TryGetValue(args.SenderSession, out var points)
            && points.Contains(msg.Point))
            return;

        Breakpoints.GetOrNew(args.SenderSession).Add(msg.Point);
        RaiseNetworkEvent(new GoapBreakpointMessage(msg.Point), args.SenderSession);
    }

    private void OnRemoveBreakpoint(
        GoapBreakpointRemoveMessage msg,
        EntitySessionEventArgs args)
    {
        if (!_admin.HasAdminFlag(args.SenderSession, AdminFlags.Debug)
            || !TryGetEntity(msg.Point.Target, out var target)
            || !HasComp<GoapComponent>(target))
            return;

        RemoveBreakpoint(args.SenderSession, msg.Point);
    }

    protected override void SendDebug(ICommonSession session, EntityUid target)
    {
        if (!_admin.HasAdminFlag(session, AdminFlags.Debug)
            || !TryComp(target, out GoapComponent? comp)
            || comp.StaticGraph == null)
            return;

        RaiseNetworkEvent(new GoapDebugInfoMessage(
                GetNetEntity(target),
                comp.PlanDebug,
                comp.StaticGraph.Value),
            session);
    }

    protected override void QueueDebugSend(ICommonSession session, EntityUid target, bool? condition = null)
    {
        if (_admin.HasAdminFlag(session, AdminFlags.Debug)
            && HasComp<GoapComponent>(target))
            DebugSendQueue.Add((session, target, condition));
    }

    protected override void BreakpointHit(
        ICommonSession session,
        GoapBreakpoint breakpoint,
        GoapPlanDebugInfo plan)
    {
        if (_admin.HasAdminFlag(session, AdminFlags.Debug)
            && TryGetEntity(breakpoint.Target, out var target)
            && HasComp<GoapComponent>(target))
            RaiseNetworkEvent(new GoapBreakpointHitMessage(breakpoint, plan), session);
    }

    /// <summary>
    /// Builds a static dependency graph from a list of executable GOAP tasks.
    /// </summary>
    /// <param name="uid">
    /// Target entity used when evaluating conditions.
    /// Required because conditions may depend on ECS state.
    /// </param>
    /// <returns>Constructed GOAP static graph.</returns>
    [PublicAPI]
    public GoapStaticGraphDebug BuildDebugGraph(EntityUid uid)
    {
        if (!TryComp(uid, out GoapComponent? comp)
            || !StaticGraphs.TryGetValue(comp.RootTask, out var graph))
            return new();

        return new GoapStaticGraphDebug(
            Nodes: graph.Nodes.Select(x => new GoapStaticGraphNodeDebug(
                    Id: x.Id,
                    Actions: x.Actions.Select(ToObject).ToList(),
                    Preconditions: x.Preconditions.Select(y => (ToObject(y), y.EntityCondition)).ToList(),
                    EffectsDump: x.Effects.GetStateDump(),
                    Compound: x.Compound))
                .ToList(),
            Edges: graph.Edges,
            OutgoingByNodeId: graph.OutgoingByNodeId,
            IncomingByNodeId: graph.IncomingByNodeId);

        ObjectDebugReflection ToObject(object obj) => _npcHelper.GetReflection(obj);
    }
}
