using System.Linq;
using System.Text;
using Content.Server.Administration;
using Content.Shared._RF.Skills;
using Content.Shared._RF.Skills.Components;
using Content.Shared._RF.Skills.Prototypes;
using Content.Shared._RF.Toolshed;
using Content.Shared.Administration;
using Robust.Shared.Prototypes;
using Robust.Shared.Toolshed;

namespace Content.Server._RF.Skills;

/// <summary>
/// The commands for the skills: giving experience and levels, and looking at how the skill checks work.
/// </summary>
/// <example>
/// <code>
/// ent 123 skills:show
/// ent 123 skills:explain MeleeAttack 456 789   (the numbers are the ids of the used entity and of the target)
/// ent 123 skills:overview 456
/// ent 123 skills:roll MeleeAttack 456 789
/// </code>
/// </example>
[ToolshedCommand, AdminCommand(AdminFlags.Debug)]
public sealed partial class SkillsCommand : SystemCommand<SkillsSystem>
{
    [Dependency] private IPrototypeManager _proto = null!;

    [CommandImplementation("add_exp")]
    public IEnumerable<EntityUid> AddExp(
        [PipedArgument] IEnumerable<EntityUid> uids,
        ProtoId<SkillPrototype> skillProto,
        int exp)
        => uids.Where(uid =>
        {
            System.AddExperience(EnsureEnt<SkillsComponent>(uid).AsNullable(), skillProto, exp);
            return true;
        });

    [CommandImplementation("set_level")]
    public IEnumerable<EntityUid> SetLevel(
        [PipedArgument] IEnumerable<EntityUid> uids,
        ProtoId<SkillPrototype> skillProto,
        int level)
        => uids.Where(uid =>
        {
            System.SetSkillLevel(EnsureEnt<SkillsComponent>(uid).AsNullable(), skillProto, level);
            return true;
        });

    /// <summary>
    /// The skills of the entity: the levels as they are and with the modifiers, and the experience.
    /// </summary>
    [CommandImplementation("show")]
    public string Show([PipedArgument] IEnumerable<EntityUid> users)
        => string.Concat(users.Select(ShowOf));

    private string ShowOf(EntityUid user)
    {
        var text = new StringBuilder();
        text.AppendLine(EntityManager.ToPrettyString(user));

        if (!EntityManager.TryGetComponent(user, out SkillsComponent? skills))
            return text.AppendLine("  no skills component").ToString();

        foreach (var data in skills.Skills.OrderBy(x => x.Id.Id))
        {
            var (flat, multiplier) = System.GetLevelModifier(user, data.Id);
            text.AppendLine($"  {data.Id}: level {data.CurrentLevel}, modifier {flat:+0;-0;0} then x{multiplier:0.##} -> " +
                            $"{System.GetModifiedLevel(user, data.Id)}/{System.GetMaxLevel(data.Id)}, " +
                            $"experience {data.CurrentExp} ({data.MinLevelExp}..{data.LevelUpExp})");
        }

        return text.ToString();
    }

    /// <summary>
    /// Everything about the skill check of the query for these participants, without doing it: which checks were looked
    /// at, which was chosen, the variables, every step of the formulas, the chances, the experience and the effects.
    /// </summary>
    /// <param name="user">The entity that performs the action.</param>
    /// <param name="query">What is being done.</param>
    /// <param name="used">An entity that serves as an intermediary between the user and the target, e.g. a tool.</param>
    /// <param name="target">The target of the action.</param>
    /// <param name="input">The usual value that the formulas of the delay and of the result are counted from.</param>
    [CommandImplementation("explain")]
    public string Explain(
        [PipedArgument] IEnumerable<EntityUid> users,
        ProtoId<SkillQueryPrototype> query,
        EntityUid used = default,
        EntityUid target = default,
        float input = 1f)
        => string.Concat(users.Select(user => ExplainOf(user, query, used, target, input)));

    private string ExplainOf(EntityUid user, ProtoId<SkillQueryPrototype> query, EntityUid used, EntityUid target, float input)
    {
        if (!TryParticipants(used, target, out var usedUid, out var targets, out var error))
            return error + "\n";

        return SkillsDebugText.Format(EntityManager, System.ExplainCheck(query, user, usedUid, targets, input));
    }

    /// <summary>
    /// One line for each query: which skill and which check the user would perform for these participants, and why not.
    /// </summary>
    [CommandImplementation("overview")]
    public string Overview([PipedArgument] IEnumerable<EntityUid> users, EntityUid used = default, EntityUid target = default)
        => string.Concat(users.Select(user => OverviewOf(user, used, target)));

    private string OverviewOf(EntityUid user, EntityUid used, EntityUid target)
    {
        if (!TryParticipants(used, target, out var usedUid, out var targets, out var error))
            return error + "\n";

        var text = new StringBuilder();
        text.AppendLine($"{EntityManager.ToPrettyString(user)}, used: " +
                        $"{(usedUid == null ? "none" : EntityManager.ToPrettyString(usedUid.Value))}, targets: " +
                        $"{(targets.Length == 0 ? "none" : string.Join(", ", targets.Select(x => EntityManager.ToPrettyString(x))))}");

        foreach (var query in _proto.EnumeratePrototypes<SkillQueryPrototype>().OrderBy(x => x.ID))
        {
            var e = System.ExplainCheck(query.ID, user, usedUid, targets);

            if (e.Result is { } r)
            {
                var variables = r.Variables.ToDictionary(x => x.Name, x => x.Value);
                text.AppendLine($"  {query.ID}: {r.Skill} / {r.Check}, level {r.ModifiedLevel}, " +
                                $"difficulty {variables[SkillCheckVariables.Difficulty]:0.##}, " +
                                $"delta {variables[SkillCheckVariables.Delta]:0.##}, " +
                                $"additional success {r.AdditionalSuccessChance:P0}, failure {r.FailProbability:P0}");
            }
            else
            {
                var reason = !e.HasSkills ? "the user has no skills"
                    : !e.Participates ? "nobody lists it in SkillDifficulty"
                    : e.Candidates.Count == 0 ? "no skill of the user handles it"
                    : "no check fits";

                text.AppendLine($"  {query.ID}: -- ({reason})");
            }
        }

        return text.ToString();
    }

    /// <summary>
    /// Really does the skill check: the experience is given and the effects are applied. The result and the experience
    /// that was given are the answer.
    /// </summary>
    [CommandImplementation("roll")]
    public string Roll(
        [PipedArgument] IEnumerable<EntityUid> users,
        ProtoId<SkillQueryPrototype> query,
        EntityUid used = default,
        EntityUid target = default)
        => string.Concat(users.Select(user => RollOf(user, query, used, target)));

    private string RollOf(EntityUid user, ProtoId<SkillQueryPrototype> query, EntityUid used, EntityUid target)
    {
        if (!TryParticipants(used, target, out var usedUid, out var targets, out var error))
            return error + "\n";

        if (!System.TryResolveCheck(query, user, usedUid, targets, out var resolved))
            return $"{EntityManager.ToPrettyString(user)}: no check for these participants, see skills:explain\n";

        System.TryGetSkillData(user, resolved.Skill, out var data);
        var before = data?.CurrentExp ?? 0;

        var result = System.DoCheck(query, user, usedUid, targets);

        System.TryGetSkillData(user, resolved.Skill, out data);
        return $"{EntityManager.ToPrettyString(user)}: {resolved.Skill} / {resolved.Id}: {result}, " +
               $"experience {(data?.CurrentExp ?? 0) - before:+0;-0;0}\n";
    }

    /// <summary>
    /// The participants of a check from the arguments: an argument that is left out is not there.
    /// </summary>
    private bool TryParticipants(EntityUid used, EntityUid target, out EntityUid? usedUid, out EntityUid[] targets, out string error)
    {
        usedUid = null;
        targets = [];
        error = string.Empty;

        if (used.IsValid())
        {
            if (!EntityManager.EntityExists(used))
            {
                error = $"no entity {used}";
                return false;
            }

            usedUid = used;
        }

        if (target.IsValid())
        {
            if (!EntityManager.EntityExists(target))
            {
                error = $"no entity {target}";
                return false;
            }

            targets = [target];
        }

        return true;
    }
}
