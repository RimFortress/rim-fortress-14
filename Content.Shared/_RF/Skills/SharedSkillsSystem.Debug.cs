using System.Linq;
using Content.Shared._RF.MathHelpers.MathCurve;
using Content.Shared._RF.Skills.Components;
using Content.Shared._RF.Skills.Prototypes;
using Content.Shared.EntityEffects;
using Content.Shared.Whitelist;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Skills;

public abstract partial class SharedSkillsSystem
{
    /// <summary>
    /// Counts everything about the skill check that is asked for, without doing it: which checks of which skills
    /// were looked at and why they fit or not, which one was chosen, the variables and every step of every formula,
    /// the chances of the results, the experience and the effects. The numbers are counted the same way that
    /// <see cref="DoCheck(ProtoId{SkillQueryPrototype},Entity{SkillsComponent?},System.Nullable{EntityUid},System.Nullable{EntityUid},System.Collections.Generic.IReadOnlyDictionary{string,float})"/>
    /// counts them. A tool for the debugging.
    /// </summary>
    /// <param name="input">The usual value for the formulas of the delay and of the result.</param>
    [PublicAPI]
    public SkillCheckExplanation ExplainCheck(
        ProtoId<SkillQueryPrototype> query,
        Entity<SkillsComponent?> user,
        EntityUid? used,
        IReadOnlyList<EntityUid> targets,
        float input = 1f,
        IReadOnlyDictionary<string, float>? variables = null)
    {
        var difficulties = new List<SkillExplainedDifficulty>
        {
            new(SkillCheckTarget.User, user, HasDifficulty(query, user), GetDifficulty(query, user)),
        };

        if (used != null)
            difficulties.Add(new(SkillCheckTarget.Used, used.Value, HasDifficulty(query, used.Value), GetDifficulty(query, used.Value)));

        foreach (var target in targets)
        {
            difficulties.Add(new(SkillCheckTarget.Target, target, HasDifficulty(query, target), GetDifficulty(query, target)));
        }

        var hasSkills = Resolve(user, ref user.Comp, false);
        var participates = hasSkills && AnyHasDifficulty(query, user, used, targets);
        var resolvedFound = TryResolveCheck(query, user, used, targets, out var resolved, variables);

        // Every check that the skills of the user have for the query, whether it fits or not
        var candidates = new List<SkillExplainedCandidate>();

        if (hasSkills)
        {
            foreach (var data in user.Comp!.Skills)
            {
                if (!Proto.TryIndex(data.Id, out var skill)
                    || !skill.Checks.TryGetValue(query, out var checks))
                    continue;

                for (var i = 0; i < checks.Count; i++)
                {
                    if (!Proto.TryIndex(checks[i], out var check))
                        continue;

                    var met = ConditionsMet(check, user, used, targets, out _);

                    candidates.Add(new SkillExplainedCandidate(
                        data.Id,
                        checks[i],
                        i,
                        check.Priority,
                        met,
                        resolvedFound && resolved!.Skill == data.Id && resolved.Id == checks[i],
                        DescribeConditions(check, user, used, targets)));
                }
            }
        }

        SkillExplainedResult? explained = null;

        if (resolvedFound)
            explained = ExplainResult(user, resolved!, input);

        return new SkillCheckExplanation(
            query,
            user,
            used,
            targets,
            difficulties,
            hasSkills,
            participates,
            candidates,
            explained);
    }

    /// <summary>
    /// The modifiers that are added to the level of the skill of the entity: the flat one that is added first and the
    /// multiplier of the sum, see <see cref="GetModifiedLevel"/>.
    /// </summary>
    [PublicAPI]
    public (int Flat, float Multiplier) GetLevelModifier(Entity<SkillsComponent?> ent, ProtoId<SkillPrototype> skill)
    {
        var ev = new GetSkillLevelModifierEvent(skill, 0, 1f);
        RaiseLocalEvent(ent, ref ev);
        return (ev.Modificator, ev.Multiplier);
    }

    private SkillExplainedResult ExplainResult(EntityUid user, ResolvedSkillCheck resolved, float input)
    {
        var check = resolved.Check;
        var variables = resolved.Variables;
        var skill = resolved.Skill;

        var baseLevel = GetLevel(user, skill);
        var levelEv = new GetSkillLevelModifierEvent(skill, 0, 1f);
        RaiseLocalEvent(user, ref levelEv);

        // The variables of the formulas before the difficulty is counted are not the same as the ones after:
        // the difficulty and the delta are not given to the difficulty formula
        var difficultyVariables = variables
            .Where(x => x.Key is not (SkillCheckVariables.Difficulty or SkillCheckVariables.Delta))
            .ToDictionary(x => x.Key, x => x.Value);

        var difficulty = Trace(check.Difficulty, 0f, user, difficultyVariables);
        var success = Trace(check.SuccessChance, 0f, user, variables);
        var fail = Trace(check.FailChance, 0f, user, variables);

        var successChance = Math.Clamp(success.Value, 0f, 1f);
        var failChance = Math.Clamp(fail.Value, 0f, 1f);

        var experience = new Dictionary<SkillCheckResult, SkillExplainedExperience>();

        foreach (var result in Enum.GetValues<SkillCheckResult>())
        {
            if (!check.Experience.TryGetValue(result, out var curves))
                continue;

            TryGetExperience(user, resolved, result, out _, out var final);
            experience[result] = new SkillExplainedExperience(Trace(curves, 0f, user, variables), final);
        }

        var effects = new List<string>();

        foreach (var (role, results) in check.Effects)
        {
            foreach (var (result, list) in results)
            {
                foreach (var effect in list)
                {
                    effects.Add($"{result} -> {role}: {DescribeEffect(effect)}");
                }
            }
        }

        return new SkillExplainedResult(
            skill,
            resolved.Id,
            baseLevel,
            levelEv.Modificator,
            levelEv.Multiplier,
            GetModifiedLevel(user, skill),
            variables.OrderBy(x => x.Key).Select(x => (x.Key, x.Value)).ToList(),
            difficulty,
            success,
            fail,
            successChance,
            (1f - successChance) * failChance,
            Trace(check.Delay, input, user, variables),
            Trace(check.Result, input, user, variables),
            input,
            experience,
            effects);
    }

    private SkillFormulaTrace Trace(IEnumerable<MathCurve> curves, float input, EntityUid user, Dictionary<string, float> variables)
    {
        var ctx = new MathCurveContext(user, variables);
        var value = input;
        var steps = new List<(string Curve, float Value)>();

        foreach (var curve in curves)
        {
            value = curve.Curve(value, _mathCurves, ctx);
            steps.Add((curve.GetType().Name, value));
        }

        return new SkillFormulaTrace(input, steps, value);
    }

    private List<string> DescribeConditions(
        SkillCheckPrototype check,
        EntityUid user,
        EntityUid? used,
        IReadOnlyList<EntityUid> targets)
    {
        var lines = new List<string>();

        void Describe(string kind, SkillCheckTarget role, EntityWhitelist list, bool whitelist)
        {
            IEnumerable<EntityUid> entities = role switch
            {
                SkillCheckTarget.User => [user],
                SkillCheckTarget.Used => used == null ? [] : [used.Value],
                _ => targets,
            };

            var entityLines = new List<string>();

            foreach (var entity in entities)
            {
                var onList = _whitelist.IsValid(list, entity);
                var fits = onList == whitelist;
                entityLines.Add($"{ToPrettyString(entity)} -> {(fits ? "fits" : "does not fit")}");
            }

            if (entityLines.Count == 0)
                entityLines.Add(whitelist ? "nobody there -> does not fit" : "nobody there -> fits");

            lines.Add($"{kind} {role} {DescribeWhitelist(list)}: {string.Join("; ", entityLines)}");
        }

        foreach (var (role, list) in check.Whitelist)
        {
            Describe("whitelist", role, list, true);
        }

        foreach (var (role, list) in check.Blacklist)
        {
            Describe("blacklist", role, list, false);
        }

        return lines;
    }

    private static string DescribeWhitelist(EntityWhitelist list)
    {
        var parts = new List<string>();

        if (list.Tags is { Count: > 0 })
            parts.Add($"tags [{string.Join(", ", list.Tags)}]");

        if (list.Components is { Length: > 0 })
            parts.Add($"components [{string.Join(", ", list.Components)}]");

        if (list.Sizes is { Count: > 0 })
            parts.Add($"sizes [{string.Join(", ", list.Sizes)}]");

        var text = string.Join(", ", parts);
        return $"{{{text}{(parts.Count > 1 ? list.RequireAll ? ", all" : ", any" : string.Empty)}}}";
    }

    private static string DescribeEffect(EntityEffect effect)
    {
        var name = effect.GetType().Name;
        return effect.Probability < 1f ? $"{name} ({effect.Probability:P0})" : name;
    }
}

/// <summary>
/// What <see cref="SharedSkillsSystem.ExplainCheck"/> found out about a skill check.
/// </summary>
/// <param name="Difficulties">What the participants have in their <see cref="SkillDifficultyComponent"/>.</param>
/// <param name="HasSkills">The user has the <see cref="SkillsComponent"/>.</param>
/// <param name="Participates">At least one of the participants lists the query, see <see cref="SharedSkillsSystem.TryResolveCheck"/>.</param>
/// <param name="Candidates">The checks of the skills of the user for the query.</param>
/// <param name="Result">The numbers of the chosen check, null if there is none.</param>
public sealed record SkillCheckExplanation(
    ProtoId<SkillQueryPrototype> Query,
    EntityUid User,
    EntityUid? Used,
    IReadOnlyList<EntityUid> Targets,
    IReadOnlyList<SkillExplainedDifficulty> Difficulties,
    bool HasSkills,
    bool Participates,
    IReadOnlyList<SkillExplainedCandidate> Candidates,
    SkillExplainedResult? Result);

public sealed record SkillExplainedDifficulty(SkillCheckTarget Role, EntityUid Entity, bool Listed, float Value);

/// <param name="Met">All the whitelists and blacklists of the check are met.</param>
/// <param name="Chosen">It is the check that is performed.</param>
/// <param name="Conditions">Every condition of the check and how the participants meet it.</param>
public sealed record SkillExplainedCandidate(
    ProtoId<SkillPrototype> Skill,
    ProtoId<SkillCheckPrototype> Check,
    int Index,
    int Priority,
    bool Met,
    bool Chosen,
    IReadOnlyList<string> Conditions);

/// <summary>
/// All the numbers of the chosen check.
/// </summary>
/// <param name="LevelModifier">The flat modifier of the level, it is added to the level before the multiplier.</param>
/// <param name="Variables">The variables that the formulas have.</param>
/// <param name="SuccessChance">The chance of the additional success as the formula gives it.</param>
/// <param name="AdditionalSuccessChance">The chance after it is held between 0 and 1.</param>
/// <param name="FailChance">The chance of failure as the formula gives it.</param>
/// <param name="FailProbability">The probability of the failure: it is rolled when there is no additional success.</param>
/// <param name="Input">The usual value that the delay and the result are counted from.</param>
public sealed record SkillExplainedResult(
    ProtoId<SkillPrototype> Skill,
    ProtoId<SkillCheckPrototype> Check,
    int BaseLevel,
    int LevelModifier,
    float LevelMultiplier,
    int ModifiedLevel,
    IReadOnlyList<(string Name, float Value)> Variables,
    SkillFormulaTrace Difficulty,
    SkillFormulaTrace SuccessChance,
    SkillFormulaTrace FailChance,
    float AdditionalSuccessChance,
    float FailProbability,
    SkillFormulaTrace Delay,
    SkillFormulaTrace Result,
    float Input,
    IReadOnlyDictionary<SkillCheckResult, SkillExplainedExperience> Experience,
    IReadOnlyList<string> Effects);

/// <summary>
/// A formula step by step: the curves, and what the value is after each.
/// </summary>
public sealed record SkillFormulaTrace(float Input, IReadOnlyList<(string Curve, float Value)> Steps, float Value);

/// <param name="Final">The experience with the modifiers of all the participants.</param>
public sealed record SkillExplainedExperience(SkillFormulaTrace Formula, int Final);
