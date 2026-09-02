using System.Linq;
using Content.Server._RF.NPC.Systems;
using Content.Server.Administration.Managers;
using Content.Shared._RF.NPC.Executable.Prototypes;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.UtilityAi;
using Content.Shared._RF.NPC.UtilityAi.Components;
using Content.Shared._RF.NPC.UtilityAi.Prototypes;
using Content.Shared._RF.NPC.UtilityAi.Systems;
using Content.Shared.Administration;
using Content.Shared.NPC;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.UtilityAi.Systems;

public sealed partial class UtilityAiSystem : SharedUtilityAiSystem
{
    [Dependency] private IAdminManager _admin = default!;
    [Dependency] private NpcHelperSystem _npcHelper = default!;

    private readonly HashSet<ProtoId<UtilityAiGoalPrototype>> _cooldownsToRemove = new();

    public override void Initialize()
    {
        base.Initialize();

#if TOOLS
        SubscribeNetworkEvent<UtilityAiDebugInfoRequest>(OnDebugInfoRequest);
#endif
    }

    private void OnDebugInfoRequest(UtilityAiDebugInfoRequest msg, EntitySessionEventArgs args)
    {
        if (!_admin.HasAdminFlag(args.SenderSession, AdminFlags.Debug)
            || !TryGetEntity(msg.Target, out var target)
            || !TryComp(target, out UtilityAiComponent? comp)
            || !TryComp(target, out GoapComponent? goap))
            return;

        RaiseNetworkEvent(new UtilityAiDebugInfoMessage(
                msg.Target,
                GetDebugInfo(new(target.Value, comp, goap))),
            args.SenderSession);
    }

    private UtilityAiDebugInfo GetDebugInfo(Entity<UtilityAiComponent, GoapComponent> ent)
    {
        var nodes = new List<UtilityAiGoalDebugInfo>();
        var edges = new List<UtilityAiStaticGraphEdge>();

        var nodeIdByProto = new Dictionary<ProtoId<UtilityAiGoalPrototype>, int>();
        var expanded = new HashSet<ProtoId<UtilityAiGoalPrototype>>();
        var edgeSet = new HashSet<(int From, int To)>();

        var agentGoals = ent.Comp1.Goals.ToHashSet();
        var executables = Proto.EnumeratePrototypes<ExecutableGoalPrototype>()
            .Select(x => x.Goal)
            .ToHashSet();

        foreach (var protoId in agentGoals)
        {
            AddGoal(protoId, ent.Comp1.CurrentGoal == protoId);
        }

        foreach (var protoId in executables)
        {
            AddGoal(protoId, ent.Comp1.CurrentGoal == protoId);
        }

        var outgoing = edges
            .GroupBy(x => x.FromNodeId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var incoming = edges
            .GroupBy(x => x.ToNodeId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return new UtilityAiDebugInfo(
            ent.Comp1.CurrentGoal,
            new UtilityAiStaticGraph(nodes, edges, outgoing, incoming));

        int AddGoal(ProtoId<UtilityAiGoalPrototype> protoId, bool inActiveBranch)
        {
            if (!Proto.Resolve(protoId, out var proto))
                return -1;

            if (!nodeIdByProto.TryGetValue(protoId, out var nodeId))
            {
                nodeId = nodes.Count;
                nodeIdByProto.Add(protoId, nodeId);
                nodes.Add(BuildGoalDebugInfo(nodeId, proto, inActiveBranch));
            }

            if (!expanded.Add(protoId))
                return nodeId;

            foreach (var fallbackId in proto.Fallbacks)
            {
                var fallbackNodeId = AddGoal(fallbackId, inActiveBranch);

                if (fallbackNodeId < 0)
                    continue;

                if (edgeSet.Add((nodeId, fallbackNodeId)))
                {
                    edges.Add(new UtilityAiStaticGraphEdge(
                        FromNodeId: nodeId,
                        ToNodeId: fallbackNodeId));
                }
            }

            return nodeId;
        }

        UtilityAiGoalDebugInfo BuildGoalDebugInfo(int id,
            UtilityAiGoalPrototype proto,
            bool inActiveBranch)
        {
            var curves = new List<UtilityAiCurveDebugDump>();
            var incumbentBonus = new List<UtilityAiCurveDebugDump>();
            var conditions = new List<UtilityAiConditionDebugDump>();
            var score = 0f;

            foreach (var condition in proto.Conditions)
            {
                var check = Goap.CheckCondition(ent, ent.Comp2.State, condition, out var dump);
                conditions.Add(new(
                    _npcHelper.GetReflection(condition),
                    dump ?? new(null, ent.Comp2.State.GetStateDump()),
                    check));
            }

            foreach (var curve in proto.ScoreCurves)
            {
                var output = Curves.Get(curve, input: score, user: ent);
                curves.Add(new(
                    _npcHelper.GetReflection(curve),
                    score,
                    output));
                score = output;
            }

            if (ent.Comp1.CurrentGoal == proto)
            {
                foreach (var curve in proto.IncumbentBonus)
                {
                    var output = Curves.Get(curve, input: score, user: ent);
                    incumbentBonus.Add(new(
                        _npcHelper.GetReflection(curve),
                        score,
                        output));
                    score = output;
                }
            }

            var penalty = ent.Comp1.Penalties.GetValueOrDefault(proto) * proto.FailPenalty;
            var ev = new UtilityAiGoalScoreModify(proto, score - penalty);
            RaiseLocalEvent(ent, ref ev);

            var result = Math.Clamp(ev.Score, 0f, 1f);

            return new UtilityAiGoalDebugInfo(
                Id: id,
                ProtoId: proto,
                Preconditions: conditions.ToArray(),
                GoalState: proto.GoalState.GetStateDump(),
                Curves: curves.ToArray(),
                IncumbentBonus: incumbentBonus.ToArray(),
                Cooldown: ent.Comp1.Cooldowns.GetValueOrDefault(proto),
                Penalty: penalty,
                Modified: ev.Score,
                Result: result,
                AgentGoal: agentGoals.Contains(proto),
                FallbackGoal: !agentGoals.Contains(proto) && !executables.Contains(proto),
                ExecutableGoal: executables.Contains(proto),
                InActiveBranch: inActiveBranch);
        }
    }

    public void UpdateNpc(ref int count, int maxUpdates)
    {
        var query = EntityQueryEnumerator<ActiveNPCComponent, UtilityAiComponent>();

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
            var ent = new Entity<UtilityAiComponent>(uid, comp);

            // If we're over our max count or it's not MapInit then ignore the NPC.
            if (updates >= maxUpdates)
            {
                // Intentional return. We don't want to go to the end logic and reset count.
                return;
            }

            Update(ent);
            count++;
            updates++;
        }

        // only reset our counter back to 0 if we finish iterating.
        // otherwise it lets us know where we left off.
        count = 0;
    }

    private void Update(Entity<UtilityAiComponent> ent)
    {
        _cooldownsToRemove.Clear();

        foreach (var (goal, time) in ent.Comp.Cooldowns)
        {
            if (time <= Timing.CurTime)
                _cooldownsToRemove.Add(goal);
        }

        foreach (var goal in _cooldownsToRemove)
        {
            ent.Comp.Cooldowns.Remove(goal);
        }

        if (ent.Comp.NextCheck > Timing.CurTime)
            return;

        ent.Comp.NextCheck = Timing.CurTime + ent.Comp.BetterGoalCheckRate;

        if (!TryGetGoal(ent.AsNullable(), out var newGoal)
            || ent.Comp.CurrentGoal == newGoal)
            return;

        SetGoal(ent.Owner, newGoal.Value);
    }
}
