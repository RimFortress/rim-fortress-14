using System.Linq;
using System.Reflection;
using System.Threading;
using Content.Server.Administration.Managers;
using Content.Server.NPC.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Prototypes;
using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared.Administration;
using Content.Shared.Mobs;
using Content.Shared.NPC;
using JetBrains.Annotations;
using Robust.Shared.CPUJob.JobQueues;
using Robust.Shared.CPUJob.JobQueues.Queues;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.GOAP.Systems;

public sealed class GoapSystem : SharedGoapSystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IAdminManager _admin = default!;
    [Dependency] private readonly NPCSystem _npc = default!;

    private readonly JobQueue _planQueue = new(0.04f);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GoapComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<GoapComponent, ComponentShutdown>(OnGoapShutdown);
        SubscribeLocalEvent<GoapComponent, MobStateChangedEvent>(_npc.OnMobStateChange);
        SubscribeLocalEvent<GoapComponent, MapInitEvent>(_npc.OnNPCMapInit);
        SubscribeLocalEvent<GoapComponent, PlayerAttachedEvent>(_npc.OnPlayerNPCAttach);
        SubscribeLocalEvent<GoapComponent, PlayerDetachedEvent>(_npc.OnPlayerNPCDetach);

        SubscribeNetworkEvent<GoapDebugInfoRequest>(OnDebugInfoRequest);

        _proto.PrototypesReloaded += args =>
        {
            if (args.WasModified<GoapCompoundPrototype>())
                ReloadPrototypes();
        };

        ReloadPrototypes();
    }

    private void OnStartup(Entity<GoapComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.State.SetValue(GoapState.Owner, ent);
        ent.Comp.ExecutableTasks = GetExecutableTasks(ent.Comp.RootTask);
    }

    private void OnGoapShutdown(Entity<GoapComponent> ent, ref ComponentShutdown args)
    {
        _npc.SleepNPC(ent);
        ent.Comp.PlanningToken?.Cancel();
        ent.Comp.PlanningJob = null;
    }

    private void OnDebugInfoRequest(GoapDebugInfoRequest request, EntitySessionEventArgs args)
    {
        if (!_admin.HasAdminFlag(args.SenderSession, AdminFlags.Debug)
            || !TryGetEntity(request.Target, out var uid)
            || !TryComp(uid, out GoapComponent? comp))
            return;

        RaiseNetworkEvent(new GoapDebugInfoMessage(
            GetNetEntity(uid.Value),
            comp.PlanDebug,
            Build(uid.Value, comp.ExecutableTasks)),
            args.SenderSession);
    }

    private void ReloadPrototypes()
    {
        var enumerator = AllEntityQuery<GoapComponent>();

        while (enumerator.MoveNext(out var comp))
        {
            comp.ExecutableTasks = GetExecutableTasks(comp.RootTask);
        }
    }

    private List<ExecutableGoapTask> GetExecutableTasks(ProtoId<GoapCompoundPrototype> protoId)
    {
        if (!_proto.Resolve(protoId, out var proto))
            return new();

        var tasks = new List<ExecutableGoapTask>();

        foreach (var task in proto.Tasks)
        {
            switch (task)
            {
                case GoapActionTask action:
                    tasks.Add(new(
                        new List<GoapAction> { action.Action },
                        action.Preconditions,
                        action.Effects,
                        protoId));
                    break;
                case GoapCompoundTask compound:
                    tasks.Add(new(
                        compound.Actions,
                        compound.Preconditions,
                        compound.Effects,
                        protoId));
                    break;
                case GoapCompoundPrototypeTask protoCompound:
                    tasks.AddRange(GetExecutableTasks(protoCompound.Proto));
                    break;
                default:
                    throw new InvalidOperationException();
            }
        }

        return tasks;
    }

    /// <summary>
    /// Request a new plan for this NPC, even if running an existing plan.
    /// </summary>
    private void RequestPlan(Entity<GoapComponent> ent)
    {
        if (ent.Comp.Planning)
            return;

        var cancelToken = new CancellationTokenSource();
        var job = new GoapPlanJob(
            0.02f,
            this,
            ent,
            ent.Comp.State,
            ent.Comp.GoalState,
            ent.Comp.ExecutableTasks,
            cancellation: cancelToken.Token);

        _planQueue.EnqueueJob(job);
        ent.Comp.PlanningJob = job;
        ent.Comp.PlanningToken = cancelToken;
        ent.Comp.NextPlanning = Timing.CurTime + ent.Comp.PlanCooldown;
    }

    public void UpdateNPC(ref int count, int maxUpdates, float frameTime)
    {
        _planQueue.Process();
        var query = EntityQueryEnumerator<ActiveNPCComponent, GoapComponent>();

        // Move ahead "count" entries in the query.
        // This is to ensure that if we didn't process all the npcs the first time,
        // we get to the remaining ones instead of iterating over the beginning again.
        for (var i = 0; i < count; i++)
        {
            query.MoveNext(out _, out _);
        }

        // the amount of updates we've processed during this iteration.
        var updates = 0;
        while (query.MoveNext(out var uid, out _, out var comp))
        {
            var ent = new Entity<GoapComponent>(uid, comp);

            // If we're over our max count or it's not MapInit then ignore the NPC.
            if (updates >= maxUpdates)
            {
                // Intentional return. We don't want to go to the end logic and reset count.
                return;
            }

            if (!comp.Enabled)
                continue;

            if (comp.PlanningJob != null)
            {
                if (comp.PlanningJob.Exception != null)
                {
                    Log.Fatal($"Received exception on planning job for {uid}!");
                    _npc.SleepNPC(uid);
                    var exc = comp.PlanningJob.Exception;
                    RemComp<GoapComponent>(uid);
                    throw exc;
                }

                // If a new planning job has finished then handle it.
                if (comp.PlanningJob.Status != JobStatus.Finished)
                    continue;

                if (comp.Plan != null)
                    PlanShutdown(ent, GoapPlanFinishReason.Interrupted);

                if (comp.PlanningJob.Result.Plan == null)
                    RaiseLocalEvent(uid, new GoapPlaningFailed(comp.GoalState));

                (comp.Plan, comp.PlanDebug) = comp.PlanningJob.Result;

                // Startup the first task and anything else we need to do.
                if (comp.Plan != null && !ActionStartup(ent, comp.Plan.Value.CurrentAction))
                    PlanShutdown(ent, GoapPlanFinishReason.Failed);

                comp.PlanningJob = null;
                comp.PlanningToken = null;
            }

            Update(ent);
            count++;
            updates++;
        }

        // only reset our counter back to 0 if we finish iterating.
        // otherwise it lets us know where we left off.
        count = 0;
    }

    private void Update(Entity<GoapComponent> ent)
    {
        if ((ent.Comp.ConstantlyReplan || ent.Comp.Plan == null) && ent.Comp.NextPlanning <= Timing.CurTime)
            RequestPlan(ent);

        // Getting a new plan so do nothing.
        if (ent.Comp.Plan == null)
            return;

        var result = GoapActionResult.Finished;

        while (result != GoapActionResult.Continuing && ent.Comp.Plan != null)
        {
            var action = ent.Comp.Plan.Value.CurrentAction;
            var plan = ent.Comp.Plan.Value;
            result = UpdateAction(ent, action);

            switch (result)
            {
                case GoapActionResult.Continuing:
                    break;
                case GoapActionResult.Failed:
                    PlanShutdown(ent, GoapPlanFinishReason.Failed);
                    break;
                // Action completed so go to the next one.
                case GoapActionResult.Finished:
                    ActionShutdown(ent, action);

                    ent.Comp.Plan = plan.MoveNext();
                    plan = ent.Comp.Plan.Value;

                    // Plan finished!
                    if (plan.Actions.Count <= plan.Index)
                    {
                        PlanShutdown(ent, GoapPlanFinishReason.Finished, false);
                        break;
                    }

                    if (!ActionStartup(ent, plan.CurrentAction))
                        PlanShutdown(ent, GoapPlanFinishReason.Failed, false);
                    break;
                default:
                    throw new InvalidOperationException();
            }
        }
    }

    #region Debug

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
    public GoapStaticGraph Build(EntityUid uid, IReadOnlyList<ExecutableGoapTask> tasks)
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

                    edges.Add(new GoapStaticGraphEdge(
                        FromNodeId: from,
                        ToNodeId: to,
                        ConditionIndex: condIndex,
                        ConditionType: condition.GetType().Name));
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

    #endregion
}
