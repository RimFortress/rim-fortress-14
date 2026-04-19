using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Prototypes;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared._RF.NPC.GOAP.Systems;

public abstract class SharedGoapSystem : EntitySystem, IGoapConditionCheсker, IGoapActionPerformer
{
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GoapComponent, ComponentStartup>(OnStartup);

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
                        new List<GoapAction>() { action.Action },
                        action.Preconditions,
                        action.Effects));
                    break;
                case GoapCompoundTask compound:
                    tasks.Add(new(compound.Actions, compound.Preconditions, compound.Effects));
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

    #region Conditions

    public bool CheckCondition<T>(EntityUid target, GoapState state, T effect, out GoapDebugDump dump) where T : BaseGoapCondition<T>
    {
        state.ReadOnly = true;
        var ev = new GoapConditionCheck<T>(effect, state, true, new());
        RaiseLocalEvent(target, ref ev);
        state.ReadOnly = false;
        dump = ev.Dump;
        return ev.Result;
    }

    /// <summary>
    /// Checks whether the GOAP target entity satisfies the condition.
    /// </summary>
    /// <param name="uid">Target entity.</param>
    /// <param name="state">
    /// The state against which the check should be performed.
    /// It may differ from the agent's actual state.
    /// </param>
    /// <param name="condition">GOAP condtition/</param>
    /// <param name="dump">Debug dump.</param>
    /// <returns>True, if the check is passed; otherwise, false</returns>
    [PublicAPI]
    public bool CheckCondition(EntityUid uid, GoapState state, GoapCondition condition, out GoapDebugDump dump)
        => condition.Check(uid, state, this, out dump);

    /// <inheritdoc cref="CheckCondition(EntityUid, GoapState, GoapCondition, out GoapDebugDump)"/>
    [PublicAPI]
    public bool CheckCondition(EntityUid uid, GoapState state, GoapCondition condition)
        => condition.Check(uid, state, this, out _);

    /// <inheritdoc cref="CheckCondition(EntityUid, GoapState, GoapCondition, out GoapDebugDump)"/>
    [PublicAPI]
    public bool CheckCondition(EntityUid uid, GoapState state, IEnumerable<GoapCondition> conditions)
    {
        foreach (var condition in conditions)
        {
            if (!CheckCondition(uid, state, condition))
                return false;
        }

        return true;
    }

    /// <inheritdoc cref="CheckCondition(EntityUid, GoapState, GoapCondition, out GoapDebugDump)"/>
    [PublicAPI]
    public bool CheckCondition(Entity<GoapComponent?> ent, GoapCondition condition)
        => Resolve(ent, ref ent.Comp) && CheckCondition(ent, ent.Comp.State, condition);

    /// <inheritdoc cref="CheckCondition(EntityUid, GoapState, GoapCondition, out GoapDebugDump)"/>
    [PublicAPI]
    public bool CheckCondition(Entity<GoapComponent?> ent, IEnumerable<GoapCondition> conditions)
        => Resolve(ent, ref ent.Comp) && CheckCondition(ent, ent.Comp.State, conditions);

    #endregion

    #region Actions

    public GoapActionResult UpdateAction<T>(EntityUid target, T action, out GoapDebugDump dump) where T : BaseGoapAction<T>
    {
        var ev = new GoapActionUpdate<T>(action, GoapActionResult.Continuing, new());
        RaiseLocalEvent(target, ref ev);
        dump = ev.Dump;
        return ev.Result;
    }

    /// <summary>
    /// Updates the execution of a GOAP action.
    /// </summary>
    /// <param name="target">Target entity.</param>
    /// <param name="action">GOAP action.</param>
    /// <returns>Update result.</returns>
    protected GoapActionResult UpdateAction(EntityUid target, GoapAction action)
    {
#if DEBUG
        var result = action.Update(target, this, out var dump);
        var comp = Comp<GoapComponent>(target);
        DebugTools.Assert(
            comp.Plan != null,
            $"attempt to update action for an agent without a plan! Agent: {ToPrettyString(target)}, Action: {action.GetType().ToString()}");
        var plan = comp.Plan.Value;
        DebugTools.Assert(plan.Actions.Count == plan.ActionsDebug?.Count);

        if (result != GoapActionResult.Continuing || dump.Dump != null) // There's no need to spam every tick about action updates
            plan.ActionsDebug[plan.Index].UpdateDumps.Add(new(Timing.CurTick, dump, result));

        return result;
#else
        return action.Update(target, this, out _);
#endif
    }

    public float ActionCost<T>(EntityUid target, GoapState state, T action) where T : BaseGoapAction<T>
    {
        state.ReadOnly = true;
        var ev = new GoapActionCost<T>(action, state, 1f);
        RaiseLocalEvent(target, ref ev);
        state.ReadOnly = false;
        return ev.Cost;
    }

    /// <summary>
    /// Calculates the cost of executing a GOAP action.
    /// </summary>
    /// <param name="target">Target entity.</param>
    /// <param name="state">
    /// The state against which the check should be performed.
    /// It may differ from the agent's actual state.
    /// </param>
    /// <param name="action">GOAP action.</param>
    /// <returns>Action execution cost.</returns>
    [PublicAPI]
    public float ActionCost(EntityUid target, GoapState state, GoapAction action)
        => action.Cost(target, state, this);

    /// <inheritdoc cref="ActionCost(EntityUid, GoapState, GoapAction)"/>
    [PublicAPI]
    public float ActionCost(Entity<GoapComponent?> ent, GoapAction action)
        => Resolve(ent, ref ent.Comp) ? ActionCost(ent, ent.Comp.State, action) : 0f;

    public void ActionStartup<T>(EntityUid target, T action, out GoapDebugDump dump) where T : BaseGoapAction<T>
    {
        var ev = new GoapActionStartup<T>(action, new());
        RaiseLocalEvent(target, ref ev);
        dump = ev.Dump;
    }

    /// <summary>
    /// Starts the action.
    /// </summary>
    /// <param name="target">Target entity.</param>
    /// <param name="action">GOAP action.</param>
    protected void ActionStartup(EntityUid target, GoapAction action)
    {
#if DEBUG
        action.Startup(target, this, out var dump);
        var comp = Comp<GoapComponent>(target);
        DebugTools.Assert(
            comp.Plan != null,
            $"attempt to startup action for an agent without a plan! Agent: {ToPrettyString(target)}, Action: {action.GetType().ToString()}");
        var plan = comp.Plan.Value;
        DebugTools.Assert(plan.Actions.Count == plan.ActionsDebug?.Count);
        var current = plan.ActionsDebug[plan.Index];
        current.StartupDump = dump;
#else
        action.Startup(target, this, out _);
#endif
    }

    public void ActionShutdown<T>(EntityUid target, T action, out GoapDebugDump dump) where T : BaseGoapAction<T>
    {
        var ev = new GoapActionShutdown<T>(action, new());
        RaiseLocalEvent(target, ref ev);
        dump = ev.Dump;
    }

    /// <summary>
    /// Finishes the action.
    /// </summary>
    /// <param name="target">Target entity.</param>
    /// <param name="action">GOAP action.</param>
    protected void ActionShutdown(EntityUid target, GoapAction action)
    {
#if DEBUG
        action.Shutdown(target, this, out var dump);
        var comp = Comp<GoapComponent>(target);
        DebugTools.Assert(
            comp.Plan != null,
            $"attempt to shutdown action for an agent without a plan! Agent: {ToPrettyString(target)}, Action: {action.GetType().ToString()}");
        var plan = comp.Plan.Value;
        DebugTools.Assert(plan.Actions.Count == plan.ActionsDebug?.Count);
        var current = plan.ActionsDebug[plan.Index];
        current.ShutdownDump = dump;
#else
        action.Shutdown(target, this, out _);
#endif
    }

    #endregion

    /// <summary>
    /// Forces the entity to perform a re-planning.
    /// </summary>
    [PublicAPI]
    public void Replan(Entity<GoapComponent?> ent)
    {
        if (Resolve(ent, ref ent.Comp))
            ent.Comp.NextPlanning = Timing.CurTime;
    }

    /// <summary>
    /// Shutdowns the current NPC plan.
    /// </summary>
    [PublicAPI]
    public void PlanShutdown(Entity<GoapComponent> ent, bool shutdownAction = true)
    {
        DebugTools.Assert(ent.Comp.Plan != null);

        if (shutdownAction)
            ActionShutdown(ent, ent.Comp.Plan.Value.CurrentAction);

        ent.Comp.Plan = null;
        ent.Comp.PlanDebug = null;
    }
}

/// <summary>
/// Used to check GOAP conditions without losing the type of condition.
/// </summary>
public interface IGoapConditionCheсker
{
    /// <summary>
    /// Checks whether the GOAP target entity satisfies the condition.
    /// </summary>
    /// <typeparam name="T">GOAP condtition type./</typeparam>
    /// <param name="target">Target entity.</param>
    /// <param name="state">
    /// The state against which the check should be performed.
    /// It may differ from the agent's actual state.
    /// </param>
    /// <param name="condition">GOAP condtition.</param>
    /// <param name="dump">Debug dump.</param>
    /// <returns>True, if the check is passed; otherwise, false</returns>
    bool CheckCondition<T>(EntityUid target, GoapState state, T condition, out GoapDebugDump dump) where T : BaseGoapCondition<T>;
}

public interface IGoapActionPerformer
{
    /// <summary>
    /// Calculates the cost of executing a GOAP action.
    /// </summary>
    /// <typeparam name="T">GOAP action type.</typeparam>
    /// <param name="target">Target entity.</param>
    /// <param name="state">
    /// The state against which the calculation should be performed.
    /// It may differ from the agent's actual state.
    /// </param>
    /// <param name="action">GOAP action.</param>
    /// <returns>Action execution cost.</returns>
    float ActionCost<T>(EntityUid target, GoapState state, T action) where T : BaseGoapAction<T>;

    /// <summary>
    /// Updates the execution of a GOAP action.
    /// </summary>
    /// <typeparam name="T">GOAP action type.</typeparam>
    /// <param name="target">Target entity.</param>
    /// <param name="action">GOAP action.</param>
    /// <param name="dump">Debug dump.</param>
    /// <returns>Update result.</returns>
    GoapActionResult UpdateAction<T>(EntityUid target, T action, out GoapDebugDump dump) where T : BaseGoapAction<T>;

    /// <summary>
    /// Starts the action.
    /// </summary>
    /// <typeparam name="T">GOAP action type.</typeparam>
    /// <param name="target">Target entity.</param>
    /// <param name="action">GOAP action.</param>
    /// <param name="dump">Debug dump.</param>
    void ActionStartup<T>(EntityUid target, T action, out GoapDebugDump dump) where T : BaseGoapAction<T>;

    /// <summary>
    /// Finishes the action.
    /// </summary>
    /// <typeparam name="T">GOAP action type.</typeparam>
    /// <param name="target">Target entity.</param>
    /// <param name="action">GOAP action.</param>
    /// <param name="dump">Debug dump.</param>
    void ActionShutdown<T>(EntityUid target, T action, out GoapDebugDump dump) where T : BaseGoapAction<T>;
}
