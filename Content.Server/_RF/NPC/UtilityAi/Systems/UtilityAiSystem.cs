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
        var goals = new List<UtilityAiGoalDebugInfo>();
        var prototypes = new HashSet<ProtoId<UtilityAiGoalPrototype>>();

        foreach (var protoId in ent.Comp1.Goals)
        {
            AddGoal(protoId);
        }

        foreach (var goal in prototypes)
        {
            var proto = Proto.Index(goal);
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

            var penalty = ent.Comp1.Penalties.GetValueOrDefault(goal) * proto.FailPenalty;
            var ev = new UtilityAiGoalScoreModify(goal, score - penalty);
            RaiseLocalEvent(ent, ref ev);
            var result = Math.Clamp(ev.Score, 0f, 1f);

            goals.Add(new(
                goal,
                conditions.ToArray(),
                proto.GoalState.GetStateDump(),
                curves.ToArray(),
                ent.Comp1.Cooldowns.GetValueOrDefault(goal),
                penalty,
                ev.Score,
                result,
                ent.Comp1.Goals.Contains(goal)));
        }

        return new UtilityAiDebugInfo(ent.Comp1.CurrentGoal, goals.ToArray());

        void AddGoal(ProtoId<UtilityAiGoalPrototype> protoId)
        {
            if (!Proto.Resolve(protoId, out var proto))
                return;

            prototypes.Add(protoId);

            foreach (var fallback in proto.Fallbacks)
            {
                AddGoal(fallback);
            }
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
