using System.Linq;
using System.Text;
using Content.Shared._RF.Skills;

namespace Content.Server._RF.Skills;

/// <summary>
/// Turns what <see cref="SharedSkillsSystem.ExplainCheck"/> found out into text for the console.
/// </summary>
public static class SkillsDebugText
{
    public static string Format(IEntityManager entMan, SkillCheckExplanation e)
    {
        var text = new StringBuilder();
        string Name(EntityUid uid) => entMan.ToPrettyString(uid);

        text.AppendLine($"Skill check {e.Query}");
        text.AppendLine($"  user: {Name(e.User)}");
        text.AppendLine($"  used: {(e.Used == null ? "none" : Name(e.Used.Value))}");
        text.AppendLine($"  targets: {(e.Targets.Count == 0 ? "none" : string.Join(", ", e.Targets.Select(Name)))}");

        text.AppendLine("Difficulty of the participants (SkillDifficulty):");

        foreach (var difficulty in e.Difficulties)
        {
            text.AppendLine($"  {difficulty.Role} {Name(difficulty.Entity)}: " +
                            (difficulty.Listed ? $"{difficulty.Value:0.##}" : "not listed (counts as 0)"));
        }

        if (!e.HasSkills)
        {
            text.AppendLine("NO CHECK: the user has no skills component");
            return text.ToString();
        }

        if (!e.Participates)
            text.AppendLine("NO CHECK: nobody lists this query in SkillDifficulty, it is not a skill matter");

        text.AppendLine("Checks of the skills of the user for this query (the chosen one is marked):");

        if (e.Candidates.Count == 0)
            text.AppendLine("  none: no skill of the user handles this query");

        foreach (var candidate in e.Candidates)
        {
            var mark = candidate.Chosen ? "[*]" : "[ ]";
            text.AppendLine($"  {mark} {candidate.Skill} / {candidate.Check}  list #{candidate.Index}, priority {candidate.Priority}, " +
                            (candidate.Met ? "conditions met" : "conditions NOT met"));

            foreach (var condition in candidate.Conditions)
            {
                text.AppendLine($"        {condition}");
            }
        }

        if (e.Result is not { } r)
        {
            if (e.Participates)
                text.AppendLine("NO CHECK: none of the checks fits");

            return text.ToString();
        }

        text.AppendLine($"Chosen: {r.Skill} / {r.Check}");
        text.AppendLine($"Level of {r.Skill}: base {r.BaseLevel}, modifier {r.LevelModifier:+0;-0;0} then x{r.LevelMultiplier:0.##} " +
                        $"-> {r.ModifiedLevel}");

        text.AppendLine("Variables:");

        foreach (var (name, value) in r.Variables)
        {
            text.AppendLine($"  {name} = {value:0.###}");
        }

        text.AppendLine("Formulas (the value after each curve):");
        text.AppendLine($"  difficulty: {Trace(r.Difficulty)}");
        text.AppendLine($"  successChance: {Trace(r.SuccessChance)}");
        text.AppendLine($"  failChance: {Trace(r.FailChance)}");
        text.AppendLine($"  delay: {Trace(r.Delay)}");
        text.AppendLine($"  result: {Trace(r.Result)}");

        var success = 1f - r.AdditionalSuccessChance - r.FailProbability;
        text.AppendLine("Chances of the results:");
        text.AppendLine($"  additional success {r.AdditionalSuccessChance:P1}, success {success:P1}, failure {r.FailProbability:P1}");
        text.AppendLine($"  (the chance of the additional success is {r.SuccessChance.Value:0.###} and of failure {r.FailChance.Value:0.###} " +
                        "by the formulas; failure is rolled when there is no additional success)");
        text.AppendLine($"For the usual value {r.Input:0.###}: delay {r.Delay.Value:0.###}, result {r.Result.Value:0.###}");

        text.AppendLine("Experience of the user for the results:");

        if (r.Experience.Count == 0)
            text.AppendLine("  none");

        foreach (var (result, experience) in r.Experience.OrderBy(x => x.Key))
        {
            text.AppendLine($"  {result}: {Trace(experience.Formula)} => {experience.Final} with the modifiers");
        }

        text.AppendLine("Effects:");

        if (r.Effects.Count == 0)
            text.AppendLine("  none");

        foreach (var effect in r.Effects)
        {
            text.AppendLine($"  {effect}");
        }

        return text.ToString();
    }

    private static string Trace(SkillFormulaTrace trace)
    {
        if (trace.Steps.Count == 0)
            return $"{trace.Input:0.###} (no formula, the input stays)";

        return $"{trace.Input:0.###} -> " + string.Join(" -> ", trace.Steps.Select(x => $"{x.Curve} {x.Value:0.###}"));
    }
}
