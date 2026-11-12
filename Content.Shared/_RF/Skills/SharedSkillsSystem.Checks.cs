using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared._RF.MathHelpers.MathCurve.Systems;
using Content.Shared._RF.Skills.Components;
using Content.Shared._RF.Skills.Prototypes;
using Content.Shared.Whitelist;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._RF.Skills;

public abstract partial class SharedSkillsSystem
{
    [Dependency] private MathCurvesSystem _mathCurves = null!;
    [Dependency] private EntityWhitelistSystem _whitelist = null!;

    private static readonly IReadOnlyList<EntityUid> NoTargets = Array.Empty<EntityUid>();

    #region Resolve

    /// <summary>
    /// Finds the skill check that the user performs for the query, see <see cref="SkillPrototype.Checks"/>.
    /// </summary>
    /// <param name="query">What is being done.</param>
    /// <param name="user">The entity that performs the action. It must have the skill that handles the query.</param>
    /// <param name="used">An entity that serves as an intermediary between the user and the targets, e.g. a tool.</param>
    /// <param name="targets">The targets of the action.</param>
    /// <param name="resolved">The chosen check with everything it needs to be calculated.</param>
    /// <param name="variables">
    /// More variables for the formulas of the check, e.g. the difficulty of a recipe. They do not replace the ones
    /// of <see cref="SkillCheckVariables"/>.
    /// </param>
    /// <returns>False if the user has no skill that performs a check for the query in these circumstances.</returns>
    /// <remarks>
    /// <para>
    /// There is no check unless at least one of the participants (the user, the used entity or one of the targets)
    /// lists the query in its <see cref="SkillDifficultyComponent"/>: an interaction that nobody has a difficulty
    /// for is not a skill matter, e.g. hitting with a crowbar or with bare fists.
    /// </para>
    /// The checks of all the skills of the user for the query whose whitelists and blacklists are met are compared by
    /// <see cref="SkillCheckPrototype.Priority"/> (the higher the better), then by their order in the list of the
    /// skill and then by the id of the skill.
    /// </remarks>
    [PublicAPI]
    public bool TryResolveCheck(
        ProtoId<SkillQueryPrototype> query,
        Entity<SkillsComponent?> user,
        EntityUid? used,
        IReadOnlyList<EntityUid> targets,
        [NotNullWhen(true)] out ResolvedSkillCheck? resolved,
        IReadOnlyDictionary<string, float>? variables = null)
    {
        resolved = null;

        if (!Resolve(user, ref user.Comp, false)
            || !AnyHasDifficulty(query, user, used, targets))
            return false;

        ProtoId<SkillPrototype> bestSkill = default;
        ProtoId<SkillCheckPrototype> bestCheckId = default;
        SkillCheckPrototype? bestCheck = null;
        var bestTargets = NoTargets;
        var bestIndex = 0;

        foreach (var data in user.Comp.Skills)
        {
            if (!Proto.TryIndex(data.Id, out var skill)
                || !skill.Checks.TryGetValue(query, out var checks))
                continue;

            for (var i = 0; i < checks.Count; i++)
            {
                if (!Proto.TryIndex(checks[i], out var check)
                    || !ConditionsMet(check, user, used, targets, out var matchedTargets))
                    continue;

                if (bestCheck != null)
                {
#if DEBUG
                    if (check.Priority == bestCheck.Priority && data.Id != bestSkill)
                    {
                        Log.Warning($"Skill query {query} is ambiguous for {ToPrettyString(user)}: checks " +
                                    $"{checks[i]} ({data.Id}) and {bestCheckId} ({bestSkill}) have the same priority.");
                    }
#endif

                    if (!IsBetter(check.Priority, i, data.Id, bestCheck.Priority, bestIndex, bestSkill))
                        continue;
                }

                bestSkill = data.Id;
                bestCheckId = checks[i];
                bestCheck = check;
                bestTargets = matchedTargets;
                bestIndex = i;
            }
        }

        if (bestCheck == null)
            return false;

        resolved = new ResolvedSkillCheck(
            bestSkill,
            bestCheckId,
            bestCheck,
            used,
            bestTargets,
            GetCheckVariables(query, user, bestSkill, bestCheck, used, bestTargets, variables));

        return true;
    }

    private static bool IsBetter(
        int priority,
        int index,
        ProtoId<SkillPrototype> skill,
        int bestPriority,
        int bestIndex,
        ProtoId<SkillPrototype> bestSkill)
    {
        if (priority != bestPriority)
            return priority > bestPriority;

        if (index != bestIndex)
            return index < bestIndex;

        return string.CompareOrdinal(skill.Id, bestSkill.Id) < 0;
    }

    /// <summary>
    /// Checks the whitelists and blacklists of the check.
    /// </summary>
    private bool ConditionsMet(
        SkillCheckPrototype check,
        EntityUid user,
        EntityUid? used,
        IReadOnlyList<EntityUid> targets,
        out IReadOnlyList<EntityUid> matchedTargets)
    {
        matchedTargets = targets;

        if (!EntityMeetsConditions(check, SkillCheckTarget.User, user)
            || !EntityMeetsConditions(check, SkillCheckTarget.Used, used))
            return false;

        var hasTargetConditions = check.Whitelist.ContainsKey(SkillCheckTarget.Target)
                                  || check.Blacklist.ContainsKey(SkillCheckTarget.Target);

        if (!hasTargetConditions)
            return true;

        var matched = new List<EntityUid>(targets.Count);

        foreach (var target in targets)
        {
            if (EntityMeetsConditions(check, SkillCheckTarget.Target, target))
                matched.Add(target);
        }

        matchedTargets = matched;
        return matched.Count > 0;
    }

    private bool EntityMeetsConditions(SkillCheckPrototype check, SkillCheckTarget role, EntityUid? uid)
    {
        check.Whitelist.TryGetValue(role, out var whitelist);
        check.Blacklist.TryGetValue(role, out var blacklist);

        // The entity is not there, so it cannot meet the whitelist, and nothing that is there can be on the blacklist
        if (uid == null)
            return whitelist == null;

        return _whitelist.IsWhitelistPassOrNull(whitelist, uid.Value)
               && _whitelist.IsWhitelistFailOrNull(blacklist, uid.Value);
    }

    private Dictionary<string, float> GetCheckVariables(
        ProtoId<SkillQueryPrototype> query,
        EntityUid user,
        ProtoId<SkillPrototype> skill,
        SkillCheckPrototype check,
        EntityUid? used,
        IReadOnlyList<EntityUid> targets,
        IReadOnlyDictionary<string, float>? extra = null)
    {
        var targetDifficulty = 0f;

        foreach (var target in targets)
        {
            targetDifficulty = Math.Max(targetDifficulty, GetDifficulty(query, target));
        }

        var level = GetModifiedLevel(user, skill);

        var variables = new Dictionary<string, float>
        {
            [SkillCheckVariables.Level] = level,
            [SkillCheckVariables.MaxLevel] = GetMaxLevel(skill),
            [SkillCheckVariables.UserDifficulty] = GetDifficulty(query, user),
            [SkillCheckVariables.UsedDifficulty] = used != null ? GetDifficulty(query, used.Value) : 0f,
            [SkillCheckVariables.TargetDifficulty] = targetDifficulty,
        };

        if (extra != null)
        {
            foreach (var (name, value) in extra)
            {
                variables.TryAdd(name, value);
            }
        }

        var difficulty = _mathCurves.Get(check.Difficulty, 0f, user, variables);

        variables[SkillCheckVariables.Difficulty] = difficulty;
        variables[SkillCheckVariables.Delta] = level - difficulty;

        return variables;
    }

    private bool AnyHasDifficulty(
        ProtoId<SkillQueryPrototype> query,
        EntityUid user,
        EntityUid? used,
        IReadOnlyList<EntityUid> targets)
    {
        if (HasDifficulty(query, user) || used != null && HasDifficulty(query, used.Value))
            return true;

        foreach (var target in targets)
        {
            if (HasDifficulty(query, target))
                return true;
        }

        return false;
    }

    private bool HasDifficulty(ProtoId<SkillQueryPrototype> query, EntityUid uid)
        => TryComp<SkillDifficultyComponent>(uid, out var comp) && comp.Difficulty.ContainsKey(query);

    private float GetDifficulty(ProtoId<SkillQueryPrototype> query, EntityUid uid)
    {
        return TryComp<SkillDifficultyComponent>(uid, out var comp)
               && comp.Difficulty.TryGetValue(query, out var difficulty)
            ? difficulty
            : 0f;
    }

    #endregion

    #region API

    /// <summary>
    /// Returns the duration of an action depending on the skill of the user.
    /// </summary>
    /// <param name="query">What is being done.</param>
    /// <param name="user">The entity that performs the action.</param>
    /// <param name="used">An entity that serves as an intermediary between the user and the target.</param>
    /// <param name="delay">The usual duration of the action, in seconds.</param>
    /// <param name="target">The target of the action, if it is known.</param>
    /// <remarks>
    /// The checks whose conditions are about the targets are not met if no target is given.
    /// </remarks>
    [PublicAPI]
    public float GetDelay(
        ProtoId<SkillQueryPrototype> query,
        Entity<SkillsComponent?> user,
        EntityUid? used,
        float delay,
        EntityUid? target = null,
        IReadOnlyDictionary<string, float>? variables = null)
        => TryResolveCheck(query, user, used, ToTargets(target), out var resolved, variables)
            ? Math.Max(0f, _mathCurves.Get(resolved.Check.Delay, delay, user, resolved.Variables))
            : delay;

    /// <inheritdoc cref="GetDelay(ProtoId{SkillQueryPrototype},Entity{SkillsComponent?},System.Nullable{EntityUid},float,System.Nullable{EntityUid},System.Collections.Generic.IReadOnlyDictionary{string,float})"/>
    [PublicAPI]
    public TimeSpan GetDelay(
        ProtoId<SkillQueryPrototype> query,
        Entity<SkillsComponent?> user,
        EntityUid? used,
        TimeSpan delay,
        EntityUid? target = null,
        IReadOnlyDictionary<string, float>? variables = null)
        => TimeSpan.FromSeconds(GetDelay(query, user, used, (float) delay.TotalSeconds, target, variables));

    /// <summary>
    /// Changes the result of an action, for example the damage of a hit or the spread of a shot,
    /// depending on the skill of the user.
    /// </summary>
    /// <param name="query">What is being done.</param>
    /// <param name="user">The entity that performs the action.</param>
    /// <param name="used">An entity that serves as an intermediary between the user and the target.</param>
    /// <param name="value">The usual result of the action.</param>
    /// <param name="target">The target of the action, if it is known.</param>
    /// <remarks>
    /// The checks whose conditions are about the targets are not met if no target is given.
    /// </remarks>
    [PublicAPI]
    public float GetResult(
        ProtoId<SkillQueryPrototype> query,
        Entity<SkillsComponent?> user,
        EntityUid? used,
        float value,
        EntityUid? target = null,
        IReadOnlyDictionary<string, float>? variables = null)
    {
        if (!TryResolveCheck(query, user, used, ToTargets(target), out var resolved, variables))
            return value;

        return _mathCurves.Get(resolved.Check.Result, value, user, resolved.Variables);
    }

    /// <summary>
    /// Performs the skill check, and according to its result gives the user experience and applies the effects of the
    /// check.
    /// </summary>
    /// <param name="query">What is being done.</param>
    /// <param name="user">The entity that performs the action.</param>
    /// <param name="used">An entity that serves as an intermediary between the user and the target.</param>
    /// <param name="target">The target of the action.</param>
    /// <returns>
    /// The result of the check, or <see cref="SkillCheckResult.Success"/> if the user performs no check for the query.
    /// </returns>
    [PublicAPI]
    public SkillCheckResult DoCheck(
        ProtoId<SkillQueryPrototype> query,
        Entity<SkillsComponent?> user,
        EntityUid? used,
        EntityUid? target = null,
        IReadOnlyDictionary<string, float>? variables = null)
        => DoCheck(query, user, used, ToTargets(target), variables);

    /// <inheritdoc cref="DoCheck(ProtoId{SkillQueryPrototype},Entity{SkillsComponent?},System.Nullable{EntityUid},System.Nullable{EntityUid},System.Collections.Generic.IReadOnlyDictionary{string,float})"/>
    /// <param name="targets">The targets of the action.</param>
    [PublicAPI]
    public SkillCheckResult DoCheck(
        ProtoId<SkillQueryPrototype> query,
        Entity<SkillsComponent?> user,
        EntityUid? used,
        IReadOnlyList<EntityUid> targets,
        IReadOnlyDictionary<string, float>? variables = null)
    {
        if (!TryResolveCheck(query, user, used, targets, out var resolved, variables))
            return SkillCheckResult.Success;

        var check = resolved.Check;
        var checkVariables = resolved.Variables;

        var successChance = Math.Clamp(_mathCurves.Get(check.SuccessChance, 0f, user, checkVariables), 0f, 1f);
        var failChance = Math.Clamp(_mathCurves.Get(check.FailChance, 0f, user, checkVariables), 0f, 1f);

        var result = Random.Prob(successChance)
            ? SkillCheckResult.AdditionalSuccess
            : Random.Prob(failChance)
                ? SkillCheckResult.Fail
                : SkillCheckResult.Success;

        GiveExperience(user, resolved, result);
        ApplyEffects(user, resolved, result);

#if DEBUG
        Log.Debug($"Skill check {resolved.Id} of the query {query}: {result}. " +
                  $"User: {ToPrettyString(user)}, used: {(used == null ? "none" : ToPrettyString(used.Value))}, " +
                  $"targets: {string.Join(", ", resolved.Targets.Select(x => ToPrettyString(x)))}, " +
                  $"skill: {resolved.Skill}, delta: {checkVariables[SkillCheckVariables.Delta]}");
#endif

        return result;
    }

    private void GiveExperience(EntityUid user, ResolvedSkillCheck resolved, SkillCheckResult result)
    {
        if (TryGetExperience(user, resolved, result, out _, out var experience))
            AddExperience(user, resolved.Skill, experience);
    }

    /// <summary>
    /// Counts the experience that the user gets for the result of the check.
    /// </summary>
    /// <param name="raw">The result of the formula of the check.</param>
    /// <param name="final">The experience with the modifiers of all the participants of the check.</param>
    private bool TryGetExperience(
        EntityUid user,
        ResolvedSkillCheck resolved,
        SkillCheckResult result,
        out float raw,
        out int final)
    {
        raw = 0f;
        final = 0;

        if (!resolved.Check.Experience.TryGetValue(result, out var curves))
            return false;

        raw = _mathCurves.Get(curves, 0f, user, resolved.Variables);

        // Query all participants in the interaction to get the final experience modifiers
        var ev = new GetSkillExpModifierEvent(resolved.Used ?? user, resolved.Skill, (int) raw, 1f);

        RaiseLocalEvent(user, ref ev);

        if (resolved.Used is { } used && used != user)
            RaiseLocalEvent(used, ref ev);

        foreach (var target in resolved.Targets)
        {
            if (target != user && target != resolved.Used)
                RaiseLocalEvent(target, ref ev);
        }

        final = (int) (ev.Modificator * ev.Multiplier);
        return true;
    }

    private void ApplyEffects(EntityUid user, ResolvedSkillCheck resolved, SkillCheckResult result)
    {
        foreach (var (role, results) in resolved.Check.Effects)
        {
            if (!results.TryGetValue(result, out var effects) || effects.Length == 0)
                continue;

            switch (role)
            {
                case SkillCheckTarget.User:
                    _entityEffects.ApplyEffects(user, effects);
                    break;
                case SkillCheckTarget.Used:
                    if (resolved.Used != null)
                        _entityEffects.ApplyEffects(resolved.Used.Value, effects);
                    break;
                case SkillCheckTarget.Target:
                    foreach (var target in resolved.Targets)
                    {
                        _entityEffects.ApplyEffects(target, effects);
                    }
                    break;
            }
        }
    }

    private static IReadOnlyList<EntityUid> ToTargets(EntityUid? target)
        => target == null ? NoTargets : new[] { target.Value };

    #endregion
}

/// <summary>
/// The names of the variables that the formulas of <see cref="SkillCheckPrototype"/> can use.
/// </summary>
public static class SkillCheckVariables
{
    /// <summary>
    /// The level of the skill of the user with all the modifiers.
    /// </summary>
    public const string Level = "level";

    /// <summary>
    /// The maximum level of the skill.
    /// </summary>
    public const string MaxLevel = "maxLevel";

    /// <summary>
    /// <see cref="SkillDifficultyComponent"/> of the user for the query.
    /// </summary>
    public const string UserDifficulty = "userDifficulty";

    /// <summary>
    /// <see cref="SkillDifficultyComponent"/> of the used entity for the query.
    /// </summary>
    public const string UsedDifficulty = "usedDifficulty";

    /// <summary>
    /// The highest <see cref="SkillDifficultyComponent"/> for the query among the targets of the check.
    /// </summary>
    public const string TargetDifficulty = "targetDifficulty";

    /// <summary>
    /// The result of <see cref="SkillCheckPrototype.Difficulty"/>. It is not given to that formula itself.
    /// </summary>
    public const string Difficulty = "difficulty";

    /// <summary>
    /// <see cref="Level"/> minus <see cref="Difficulty"/>. It is not given to
    /// <see cref="SkillCheckPrototype.Difficulty"/> itself.
    /// </summary>
    public const string Delta = "delta";
}

/// <summary>
/// A skill check that the user performs for a query, see <see cref="SharedSkillsSystem.TryResolveCheck"/>.
/// </summary>
/// <param name="Skill">The skill that is checked.</param>
/// <param name="Id">The id of the check prototype.</param>
/// <param name="Check">The check prototype.</param>
/// <param name="Used">An entity that serves as an intermediary between the user and the targets.</param>
/// <param name="Targets">The targets of the action that the check can be performed on.</param>
/// <param name="Variables">The variables of the formulas of the check.</param>
public sealed record ResolvedSkillCheck(
    ProtoId<SkillPrototype> Skill,
    ProtoId<SkillCheckPrototype> Id,
    SkillCheckPrototype Check,
    EntityUid? Used,
    IReadOnlyList<EntityUid> Targets,
    Dictionary<string, float> Variables);
