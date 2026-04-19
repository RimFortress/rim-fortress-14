using System.Threading;
using Content.Server.NPC.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared.Mobs;
using Content.Shared.NPC;
using Robust.Shared.CPUJob.JobQueues;
using Robust.Shared.CPUJob.JobQueues.Queues;
using Robust.Shared.Player;

namespace Content.Server._RF.NPC.GOAP.Systems;

public sealed class GoapSystem : SharedGoapSystem
{
    [Dependency] private readonly NPCSystem _npc = default!;

    private JobQueue _planQueue = new(0.04f);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GoapComponent, ComponentShutdown>(OnGoapShutdown);
        SubscribeLocalEvent<GoapComponent, MobStateChangedEvent>(_npc.OnMobStateChange);
        SubscribeLocalEvent<GoapComponent, MapInitEvent>(_npc.OnNPCMapInit);
        SubscribeLocalEvent<GoapComponent, PlayerAttachedEvent>(_npc.OnPlayerNPCAttach);
        SubscribeLocalEvent<GoapComponent, PlayerDetachedEvent>(_npc.OnPlayerNPCDetach);
    }

    private void OnGoapShutdown(Entity<GoapComponent> ent, ref ComponentShutdown args)
    {
        _npc.SleepNPC(ent);
        ent.Comp.PlanningToken?.Cancel();
        ent.Comp.PlanningJob = null;
    }

    /// <summary>
    /// Request a new plan for this NPC, even if running an existing plan.
    /// </summary>
    private void RequestPlan(Entity<GoapComponent> ent)
    {
        if (ent.Comp.Planning)
            return;

        if (ent.Comp.GoalState.Count == 0)
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
                    PlanShutdown(ent);

                (comp.Plan, comp.PlanDebug) = comp.PlanningJob.Result;

                // Startup the first task and anything else we need to do.
                if (comp.Plan != null)
                    ActionStartup(ent, comp.Plan.Value.CurrentAction);

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
                    PlanShutdown(ent);
                    break;
                // Action completed so go to the next one.
                case GoapActionResult.Finished:
                    ActionShutdown(ent, action);
                    plan.Index++;

                    // Plan finished!
                    if (plan.Actions.Count <= plan.Index)
                    {
                        PlanShutdown(ent, false);
                        break;
                    }

                    ActionStartup(ent, plan.CurrentAction);
                    break;
                default:
                    throw new InvalidOperationException();
            }
        }
    }
}
