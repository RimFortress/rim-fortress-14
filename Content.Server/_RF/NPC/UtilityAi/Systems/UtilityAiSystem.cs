using System.Linq;
using System.Reflection;
using Content.Server.Administration.Managers;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.UtilityAi;
using Content.Shared._RF.NPC.UtilityAi.Components;
using Content.Shared._RF.NPC.UtilityAi.Prototypes;
using Content.Shared._RF.NPC.UtilityAi.Systems;
using Content.Shared.Administration;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.UtilityAi.Systems;

public sealed class UtilityAiSystem : SharedUtilityAiSystem
{
    [Dependency] private readonly IAdminManager _admin = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<UtilityAiDebugInfoRequest>(OnDebugInfoRequest);
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
            GetDebugInfo(new(target.Value, comp, goap))));
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
            var conditions = new List<UtilityAiConditionDebugDump>();
            var score = 0f;

            foreach (var condition in proto.Conditions)
            {
                var check = Goap.CheckCondition(ent, ent.Comp2.State, condition, out var dump);
                conditions.Add(new(
                    GetReflection(condition),
                    condition.GetType().Name,
                    dump ?? new(null, ent.Comp2.State.GetStateDump()),
                    check));
            }

            foreach (var curve in proto.ScoreCurves)
            {
                var output = Curves.Get(curve, input: score, user: ent);
                curves.Add(new(
                    GetReflection(curve),
                    curve.GetType().Name,
                    score,
                    output));
                score = output;
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

    private static Dictionary<string, (string, string)> GetReflection(object obj)
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

        return reflection;
    }
}
