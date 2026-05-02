using System.Linq;
using System.Reflection;
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
        SubscribeNetworkEvent<GoapBreakpointMessage>(OnBreakpoint);
        SubscribeNetworkEvent<GoapBreakpointRemoveMessage>(OnRemoveBreakpoint);
    }

    private void OnDebugInfoRequest(GoapDebugInfoRequest request, EntitySessionEventArgs args)
    {
        SendDebug(args.SenderSession, GetEntity(request.Target));
    }

    private void OnBreakpoint(GoapBreakpointMessage msg, EntitySessionEventArgs args)
    {
        if (!_admin.HasAdminFlag(args.SenderSession, AdminFlags.Debug)
            || !TryGetEntity(msg.Point.Target, out var target)
            || !HasComp<GoapComponent>(target))
            return;

        // Holy shit
        if (msg.Point.Kind is GoapBreakpointKind.Precondition or GoapBreakpointKind.ActionStartup
            && msg.Point.Result != GoapBreakpointResultKind.True
            && msg.Point.Result != GoapBreakpointResultKind.False
            || msg.Point.Kind == GoapBreakpointKind.ActionUpdate
            && msg.Point.Result != GoapBreakpointResultKind.Continuing
            && msg.Point.Result != GoapBreakpointResultKind.Failed
            && msg.Point.Result != GoapBreakpointResultKind.Finished
            || msg.Point.Kind == GoapBreakpointKind.ActionShutdown
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

    protected override void SendDebug(
        ICommonSession session,
        EntityUid target,
        GoapBreakpoint? breakpoint = null)
    {
        if (!_admin.HasAdminFlag(session, AdminFlags.Debug)
            || !TryComp(target, out GoapComponent? comp)
            || comp.StaticGraph == null)
            return;

        RaiseNetworkEvent(new GoapDebugInfoMessage(
                GetNetEntity(target),
                comp.PlanDebug,
                comp.StaticGraph.Value,
                breakpoint),
            session);
    }

    protected override void QueueDebugSend(
        ICommonSession session,
        EntityUid target,
        GoapBreakpoint? breakpoint = null)
    {
        if (_admin.HasAdminFlag(session, AdminFlags.Debug)
            && HasComp<GoapComponent>(target))
            DebugSendQueue.Add((session, target, breakpoint));
    }

    /// <summary>
    /// Builds a static dependency graph from a list of executable GOAP tasks.
    /// </summary>
    /// <param name="uid">
    /// Target entity used when evaluating conditions.
    /// Required because conditions may depend on ECS state.
    /// </param>
    /// <param name="tasks">List of executable tasks.</param>
    /// <returns>Constructed GOAP static graph.</returns>
    [PublicAPI]
    public GoapStaticGraph BuildStaticGraph(EntityUid uid, IReadOnlyList<ExecutableGoapTask> tasks)
    {
        var edges = new List<GoapStaticGraphEdge>();

        // Create graph nodes
        var nodes = tasks.Select((task, i) => new GoapStaticGraphNode(
                Id: i,
                Actions: task.Actions.Select(ToObject).ToList(),
                Preconditions: task.Preconditions.Select(ToObject).ToList(),
                EffectsDump: task.Effects.GetStateDump()))
            .ToList();

        // Build edges by checking condition satisfaction
        for (var to = 0; to < tasks.Count; to++)
        {
            var consumer = tasks[to];

            // Iterate over each precondition of the consumer task
            for (var condIndex = 0; condIndex < consumer.Preconditions.Count; condIndex++)
            {
                var condition = consumer.Preconditions[condIndex];

                // Try all possible producers
                for (var from = 0; from < tasks.Count; from++)
                {
                    if (from == to)
                        continue;

                    var producer = tasks[from];

                    // We perform two checks: the first when the state is empty,
                    // and the second on the node's effects.
                    // This is done to verify that the effects and conditions actually
                    // link the two nodes, rather than the second node simply having no conditions.
                    var dummyState = new GoapState();
                    dummyState.SetValue(GoapState.Owner, uid);
                    var dummyCheck = CheckCondition(uid, dummyState, condition);

                    var effectsState = producer.Effects.ShallowClone();
                    effectsState.SetValue(GoapState.Owner, uid);
                    var effectsCheck = CheckCondition(uid, effectsState, condition);

                    if (!effectsCheck || effectsCheck == dummyCheck)
                        continue;

                    edges.Add(new GoapStaticGraphEdge(from, to));
                }
            }
        }

        // Build lookup dictionaries for fast graph traversal
        var outgoing = edges
            .GroupBy(x => x.FromNodeId)
            .ToDictionary(
                g => g.Key,
                g => g.ToList());

        var incoming = edges
            .GroupBy(x => x.ToNodeId)
            .ToDictionary(
                g => g.Key,
                g => g.ToList());

        return new GoapStaticGraph(
            Nodes: nodes,
            Edges: edges,
            OutgoingByNodeId: outgoing,
            IncomingByNodeId: incoming);
    }

    private static GoapStaticGraphObject ToObject(object obj)
    {
        var type = obj.GetType();
        var fields = type
            .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(f => !f.IsStatic && f.IsDefined(typeof(DataFieldAttribute), inherit: true));
        var reflection = new Dictionary<string, (string, string)>();

        foreach (var field in fields)
        {
            try
            {
                reflection.Add(
                    field.Name,
                    (field.FieldType.Name,
                    field.GetValue(obj)?.ToString() ?? "null"));
            }
            catch (Exception e)
            {
                reflection.Add(
                    field.Name,
                    (field.FieldType.Name,
                    $"<error: {e.GetType().Name}, {e.Message}>"));
            }
        }

        return new(type.Name, reflection);
    }
}
