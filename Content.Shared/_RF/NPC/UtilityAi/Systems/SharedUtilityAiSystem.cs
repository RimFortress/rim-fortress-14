using System.Diagnostics.CodeAnalysis;
using Content.Shared._RF.MathHelpers.MathCurve.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared._RF.NPC.UtilityAi.Components;
using Content.Shared._RF.NPC.UtilityAi.Prototypes;
using JetBrains.Annotations;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._RF.NPC.UtilityAi.Systems;

/// <summary>
/// A system that manages GOAP NPCs using Utility AI to find a goal state.
/// </summary>
public abstract class SharedUtilityAiSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] protected readonly IPrototypeManager Proto = default!;
    [Dependency] protected readonly SharedGoapSystem Goap = default!;
    [Dependency] protected readonly MathCurvesSystem Curves = default!;
    [Dependency] private readonly SharedNpcSearcherSystem _searcher = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<UtilityAiComponent, GoapPlaningFailed>(OnGoapPlaningFailed);
        SubscribeLocalEvent<UtilityAiComponent, GoapPlanFinished>(OnGoapPlanFinished);
    }

    private void OnGoapPlaningFailed(Entity<UtilityAiComponent> ent, ref GoapPlaningFailed args)
    {
        ReleaseCaptured(ent.Owner);
        DoGoalFail(ent);
    }

    private void OnGoapPlanFinished(Entity<UtilityAiComponent> ent, ref GoapPlanFinished args)
    {
        // Removing temp keys
        if (Proto.TryIndex(ent.Comp.CurrentGoal, out var proto)
            && TryComp(ent, out GoapComponent? goap))
        {
            foreach (var key in proto.TempKeys)
            {
                Goap.RemoveKey<object>(goap.State, key);
            }
        }

        // Allow other systems to take over the logic of selecting a new goal
        if (ent.Comp.CurrentGoal != null)
        {
            var ev = new BeforeUtilityAiGoalFinished(ent.Comp.CurrentGoal.Value,
                args.Reason switch
                {
                    GoapPlanFinishReason.Finished => UtilityAiGoalFinishReason.Finished,
                    GoapPlanFinishReason.Failed => UtilityAiGoalFinishReason.Failed,
                    GoapPlanFinishReason.Interrupted => UtilityAiGoalFinishReason.Interrupted,
                    _ => throw new ArgumentOutOfRangeException(),
                },
                false);
            RaiseLocalEvent(ent, ref ev);

            if (ev.Handled)
            {
                ReleaseCaptured(ent.Owner);
                return;
            }
        }

        ReleaseCaptured(ent.Owner);

        switch (args.Reason)
        {
            case GoapPlanFinishReason.Finished:
                if (ent.Comp.CurrentGoal != null)
                {
                    RaiseLocalEvent(ent,
                        new UtilityAiGoalFinished(ent.Comp.CurrentGoal.Value, UtilityAiGoalFinishReason.Finished));
                }

                ent.Comp.Penalties.Clear();

                if (TryGetGoal(ent.AsNullable(), out var goal))
                    SetGoal(ent.Owner, goal.Value);
                else
                {
                    ent.Comp.CurrentGoal = null;
                    Dirty(ent);
                }

                break;
            case GoapPlanFinishReason.Failed:
                DoGoalFail(ent);
                break;
            case GoapPlanFinishReason.Interrupted:
                if (ent.Comp.CurrentGoal != null)
                {
                    RaiseLocalEvent(ent,
                        new UtilityAiGoalFinished(ent.Comp.CurrentGoal.Value, UtilityAiGoalFinishReason.Interrupted));
                }
                ent.Comp.Penalties.Clear();
                ent.Comp.CurrentGoal = null;
                Dirty(ent);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void DoGoalFail(Entity<UtilityAiComponent> ent)
    {
        if (ent.Comp.CurrentGoal == null)
        {
            if (TryGetGoal(ent.AsNullable(), out var newGoal))
                SetGoal(ent.Owner, newGoal.Value);
            else
            {
                ent.Comp.CurrentGoal = null;
                Dirty(ent);
            }

            return;
        }

        RaiseLocalEvent(ent,
            new UtilityAiGoalFinished(ent.Comp.CurrentGoal.Value, UtilityAiGoalFinishReason.Failed));

        if (!Proto.Resolve(ent.Comp.CurrentGoal, out var proto))
            return;

        foreach (var fallback in proto.Fallbacks)
        {
            if (!ConditionsMet(ent.Owner, fallback))
                continue;

            SetGoal(ent.Owner, fallback);
            return;
        }

        switch (proto.FailPolicy)
        {
            case UtilityAiFailPolicy.Cooldown:
                ent.Comp.Cooldowns[proto] = _timing.CurTime + proto.FailCooldown;
                break;
            case UtilityAiFailPolicy.Penalty:
                if (!ent.Comp.Penalties.TryAdd(proto, 1))
                    ent.Comp.Penalties[proto]++;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        ent.Comp.CurrentGoal = null;
        Dirty(ent);

        if (TryGetGoal(ent.AsNullable(), out var goal))
            SetGoal(ent.Owner, goal.Value);
    }

    /// <summary>
    /// Releases entities captured by goal.
    /// </summary>
    [PublicAPI]
    public void ReleaseCaptured(Entity<UtilityAiComponent?, GoapComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp1, ref ent.Comp2)
            || !Proto.TryIndex(ent.Comp1.CurrentGoal, out var proto))
            return;

        foreach (var key in proto.Capture)
        {
            if (Goap.RemoveKey(ent.Comp2.State, key, out var value))
                _searcher.ReleaseCapturedResult(value, ent);
        }
    }

    /// <summary>
    /// Sets the agent's current GOAP goal.
    /// </summary>
    /// <param name="ent">GOAP agent entity.</param>
    /// <param name="protoId">Goal prototype.</param>
    [PublicAPI]
    public void SetGoal(Entity<UtilityAiComponent?, GoapComponent?> ent, ProtoId<UtilityAiGoalPrototype> protoId)
    {
        if (!Proto.Resolve(protoId, out var proto)
            || !Resolve(ent, ref ent.Comp1, ref ent.Comp2))
            return;

        foreach (var key in proto.Capture)
        {
            if (!Goap.TryGetValue(ent.Comp2.State, key, out var value))
                continue;

            Goap.SetValue(ent.Comp2.State, key, value);
            _searcher.CaptureResult(value, ent);
        }

        Goap.SetGoal(new(ent, ent.Comp2), proto.GoalState);
        ent.Comp1.CurrentGoal = protoId;
        Dirty(ent, ent.Comp1);
        RaiseLocalEvent(ent, new UtilityAiGoalGiven(protoId));
    }

    /// <summary>
    /// Returns the agent's current Utility Ai goal.
    /// </summary>
    /// <param name="ent">GOAP agent entity.</param>
    /// <param name="protoId">Current goal prototype.</param>
    /// <returns></returns>
    [PublicAPI, Pure]
    public bool TryGetCurrentGoal(
        Entity<UtilityAiComponent?> ent,
        [NotNullWhen(true)] out ProtoId<UtilityAiGoalPrototype>? protoId)
    {
        protoId = null;

        if (!Resolve(ent, ref ent.Comp) || ent.Comp.CurrentGoal == null)
            return false;

        protoId = ent.Comp.CurrentGoal.Value;
        return true;
    }

    /// <summary>
    /// Searches for the best available goal for the GOAP agent to achieve.
    /// </summary>
    /// <param name="ent">GOAP agent entity.</param>
    /// <param name="protoId">Found goal prototype.</param>
    /// <returns>True, if the goal is found; otherwise, false.</returns>
    [PublicAPI, Pure]
    public bool TryGetGoal(
        Entity<UtilityAiComponent?> ent,
        [NotNullWhen(true)] out ProtoId<UtilityAiGoalPrototype>? protoId)
    {
        protoId = null;

        if (!Resolve(ent, ref ent.Comp))
            return false;

        (ProtoId<UtilityAiGoalPrototype> Proto, float Score)? max = null;

        foreach (var goal in ent.Comp.Goals)
        {
            if (ent.Comp.Cooldowns.ContainsKey(goal)
                || !ConditionsMet(ent.Owner, goal))
                continue;

            var score = GetScore(ent, goal);

            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (score == 1f)
            {
                protoId = goal;
                return true;
            }

            if (max == null || max.Value.Score < score)
                max = (goal, score);
        }

        if (max == null)
            return false;

        protoId = max.Value.Proto;
        return true;
    }

    /// <summary>
    /// Returns a goal score between 0 and 1 for the GOAP agent.
    /// </summary>
    /// <param name="ent">GOAP agent entity.</param>
    /// <param name="protoId">Goal prototype.</param>
    [PublicAPI, Pure]
    public float GetScore(
        Entity<UtilityAiComponent?> ent,
        ProtoId<UtilityAiGoalPrototype> protoId)
    {
        if (!Resolve(ent, ref ent.Comp) || !Proto.Resolve(protoId, out var proto))
            return 0f;

        var score = Curves.Get(proto.ScoreCurves, user: ent);

        if (ent.Comp.CurrentGoal == protoId)
            score = Curves.Get(proto.IncumbentBonus, user: ent, input: score);

        var penalty = ent.Comp.Penalties.GetValueOrDefault(protoId) * proto.FailPenalty;
        var ev = new UtilityAiGoalScoreModify(protoId, score - penalty);

        RaiseLocalEvent(ent, ref ev);
        return Math.Clamp(ev.Score, 0f, 1f);
    }

    /// <summary>
    /// Checks conditions for the agent's ability to achieve the goal.
    /// </summary>
    /// <param name="ent">GOAP agent entity.</param>
    /// <param name="protoId">Goal prototype.</param>
    /// <returns></returns>
    [PublicAPI, Pure]
    public bool ConditionsMet(
        Entity<GoapComponent?> ent,
        ProtoId<UtilityAiGoalPrototype> protoId)
        => Proto.Resolve(protoId, out var proto)
           && Goap.CheckCondition(ent, proto.Conditions);

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_net.IsServer)
            return;

        var enumerator = EntityQueryEnumerator<UtilityAiComponent>();
        while (enumerator.MoveNext(out var comp))
        {
            var toRemove = new List<ProtoId<UtilityAiGoalPrototype>>();

            foreach (var (goal, time) in comp.Cooldowns)
            {
                if (time <= _timing.CurTime)
                    toRemove.Add(goal);
            }

            foreach (var goal in toRemove)
            {
                comp.Cooldowns.Remove(goal);
            }
        }
    }
}
