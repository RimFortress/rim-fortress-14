using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared._RF.Skills.Components;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._RF.Skills;

/// <summary>
/// Manages <see cref="SkillsComponent"/>
/// </summary>
public abstract class SharedSkillsSystem : EntitySystem
{
    [Dependency] protected readonly IPrototypeManager Proto = default!;
    [Dependency] protected readonly IRobustRandom Random = default!;

    public const string DefaultSkillProfession = "skill-profession-default";

    protected ISawmill Sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();

        Sawmill = LogManager.GetSawmill("skills");
    }

    /// <summary>
    /// Returns the profession name of the entity with the highest skill level
    /// </summary>
    /// <remarks>
    /// Returns the default profession name if no matching one is found
    /// </remarks>
    public string SkillProfession(Entity<SkillsComponent?> ent)
    {
        var defaultProf = Loc.GetString(DefaultSkillProfession);

        if (!Resolve(ent, ref ent.Comp))
            return defaultProf;

        (ProtoId<SkillPrototype> Skill, int Level)? bestSkill = null;

        foreach (var data in ent.Comp.Skills)
        {
            if (data.CurrentLevel == 0
                || !Proto.TryIndex(data.Id, out var proto)
                || proto.Profession == null)
                continue;

            if (bestSkill != null && bestSkill.Value.Level >= data.CurrentLevel)
                continue;

            bestSkill = (proto, data.CurrentLevel);
        }

        if (bestSkill == null)
            return defaultProf;

        return SkillProfession(ent, bestSkill.Value.Skill) ?? defaultProf;
    }

    /// <summary>
    /// Returns the profession name for the given skill according to the current level
    /// </summary>
    public string? SkillProfession(Entity<SkillsComponent?> ent, ProtoId<SkillPrototype> skill)
    {
        if (!Proto.TryIndex(skill, out var proto) || proto.Profession == null)
            return null;

        var level = GetLevel(ent, skill);
        var prefixes = proto.LevelPrefixes
            .Where(x => x.Key <= level)
            .Select(x => x.Key)
            .ToList();

        var profession = $"[color={proto.Color.ToHex()}]{Loc.GetString(proto.Profession)}[/color]";

        if (prefixes.Count == 0)
            return profession;

        var prefix = proto.LevelPrefixes[prefixes.Max()];

        return $"{Loc.GetString(prefix)} {profession}";
    }

    /// <summary>
    /// Returns the current skill level of the entity
    /// </summary>
    public int GetLevel(Entity<SkillsComponent?> ent, ProtoId<SkillPrototype> skill)
    {
        if (!Resolve(ent, ref ent.Comp)
            || ent.Comp.Skills.FirstOrDefault(x => x.Id == skill) is not { } data)
            return 0;

        return data.CurrentLevel;
    }

    /// <summary>
    /// Calculates the skill level for a given amount of experience
    /// </summary>
    public int GetLevel(ProtoId<SkillPrototype> skill, int experience)
    {
        if (!Proto.TryIndex(skill, out var proto)
            || proto.LevelExpMultiplier <= 0
            || experience < 0)
            return 0;

        // When LevelExpMultiplier == 1 (linear progression)
        if (Math.Abs(proto.LevelExpMultiplier - 1.0) < 1e-10)
            return experience / proto.LevelUpExp;

        // Sₙ = a₁ * (qⁿ - 1) / (q - 1) <- geometric progression sum formula
        // n - skill level
        // here we solve the equation with respect to n
        var level = Math.Log(
            experience * (proto.LevelExpMultiplier - 1) / proto.LevelUpExp + 1,
            proto.LevelExpMultiplier);

        return Math.Min((int) Math.Floor(level), proto.MaxLevel);
    }

    /// <summary>
    /// Returns the maximum amount of experience for the given skill level
    /// </summary>
    public int GetLevelMaxPoints(ProtoId<SkillPrototype> skill, int level)
    {
        if (!Proto.TryIndex(skill, out var proto))
            return 0;

        level = Math.Clamp(level, 0, proto.MaxLevel) + 1;

        // If I hadn't skipped school I wouldn't have had
        // to search for the sum of geometric progression formula
        // Sₙ = a₁ * (qⁿ - 1) / (q - 1)
        return (int) (proto.LevelUpExp
                      * (Math.Pow(proto.LevelExpMultiplier, level) - 1)
                      / (proto.LevelExpMultiplier - 1));
    }

    /// <summary>
    /// Returns the minimum amount of experience for the given skill level
    /// </summary>
    public int GetLevelMinPoints(ProtoId<SkillPrototype> skill, int level)
    {
        return level <= 0 ? 0 : GetLevelMaxPoints(skill, level - 1);
    }

    public bool TryGetSkillData(
        Entity<SkillsComponent?> ent,
        ProtoId<SkillPrototype> skill,
        [NotNullWhen(true)] out SkillData? data)
    {
        data = null;

        if (!Resolve(ent, ref ent.Comp) || ent.Comp.Skills.FirstOrDefault(x => x.Id == skill) is not { } skillData)
            return false;

        data = skillData;
        return true;
    }

    /// <summary>
    /// Adds experience points for the skill of the given entity
    /// </summary>
    public void AddExperience(Entity<SkillsComponent?> ent, ProtoId<SkillPrototype> skill, int amount, bool dirty = true)
    {
        if (!Resolve(ent, ref ent.Comp, false)
            || !Proto.TryIndex(skill, out var proto)
            || amount == 0)
            return;

        if (!TryGetSkillData(ent, skill, out var data) && !AddSkill(ent, skill, out data))
            return;

        var oldLevel = data.CurrentLevel;

        data.CurrentExp += (int) (amount * data.ExpFactor * ent.Comp.ExpFactor);

        if (data.CurrentExp > data.LevelUpExp && data.CurrentLevel == proto.MaxLevel)
            data.CurrentExp = data.LevelUpExp;

        data.CurrentLevel = GetLevel(data.Id, data.CurrentExp);
        data.LevelUpExp = GetLevelMaxPoints(data.Id, data.CurrentLevel);
        data.MinLevelExp = GetLevelMinPoints(data.Id, data.CurrentLevel);

        if (dirty)
            Dirty(ent);

        if (oldLevel == data.CurrentLevel)
            return;

        var args = new EntityEffectBaseArgs(ent, EntityManager);

        if (proto.LevelUpEffects.TryGetValue(0, out var zeroEffects))
        {
            foreach (var effect in zeroEffects)
            {
                if (effect.ShouldApply(args, Random))
                    effect.Effect(args);
            }
        }

        if (data.CurrentLevel != 0 && proto.LevelUpEffects.TryGetValue(data.CurrentLevel, out var effects))
        {
            foreach (var effect in effects)
            {
                if (effect.ShouldApply(args, Random))
                    effect.Effect(args);
            }
        }

        RaiseLocalEvent(ent, new SkillLevelChanged(data.CurrentLevel, oldLevel));
    }

    /// <summary>
    /// Adds a skill for an entity
    /// </summary>
    protected bool AddSkill(
        Entity<SkillsComponent?> ent,
        ProtoId<SkillPrototype> skill,
        [NotNullWhen(true)] out SkillData? data,
        bool dirty = true)
    {
        data = null;

        if (!Resolve(ent, ref ent.Comp)
            || ent.Comp.Skills.Any(x => x.Id == skill.Id)
            || !Proto.TryIndex(skill, out var proto))
            return false;

        data = new SkillData
        {
            Id = skill,
            CurrentLevel = 0,
            CurrentExp = 0,
            LevelUpExp = proto.LevelUpExp,
            MinLevelExp = 0,
        };

        ent.Comp.Skills.Add(data);

        if (dirty)
            Dirty(ent);

        return true;
    }


    #region Interactions

    /// <summary>
    /// Changes some input number depending on the result of the skill check for the interaction
    /// </summary>
    public int GetInteractionResult(Entity<SkillInteractionComponent?> ent, Entity<SkillsComponent?> user, int value)
    {
        return (int) Math.Floor(GetInteractionResult(ent, user, (float) value));
    }

    /// <summary>
    /// Changes some input number depending on the result of the skill check for the interaction
    /// </summary>
    public float GetInteractionResult(Entity<SkillInteractionComponent?> ent, Entity<SkillsComponent?> user, float value)
    {
        if (!Resolve(ent, ref ent.Comp, false)
            || !Resolve(user, ref user.Comp, false))
            return value;

        var delta = GetLevel(user, ent.Comp.Skill) - ent.Comp.TargetLevel;

        var result = delta switch
        {
            0 => value,
            > 0 => value + delta * ent.Comp.ResultFactor,
            < 0 => value - delta * ent.Comp.ResultFactor,
        };

        return Math.Clamp(result, ent.Comp.MinResult, ent.Comp.MaxResult);
    }

    /// <summary>
    /// Returns the duration of interaction execution depending on the current skill level
    /// </summary>
    public TimeSpan GetDelay(Entity<SkillInteractionComponent?> ent,
        Entity<SkillsComponent?> user,
        TimeSpan delay)
    {
        return TimeSpan.FromSeconds(GetDelay(ent, user, (float) delay.TotalSeconds));
    }

    /// <summary>
    /// Returns the duration of interaction execution depending on the current skill level
    /// </summary>
    public float GetDelay(Entity<SkillInteractionComponent?> ent, Entity<SkillsComponent?> user, float delay)
    {
        if (!Resolve(ent, ref ent.Comp) || !Resolve(user, ref user.Comp))
            return delay;

        var delta = GetLevel(user, ent.Comp.Skill) - ent.Comp.TargetLevel;

        return Math.Clamp(delay - delta * ent.Comp.DoAfterFactor,
            ent.Comp.MinDoAfterTime,
            ent.Comp.MaxDoAfterTime);
    }

    /// <summary>
    /// Performs skill checks for interaction and, according to the result of the check,
    /// gives out experience and triggers interaction effects
    /// </summary>
    /// <param name="ent">Entity on which the interaction is performed</param>
    /// <param name="user">The user who performs the interaction</param>
    /// <param name="target">Target entity of the interaction</param>
    /// <returns>Skills check result for interaction</returns>
    public SkillCheckResult DoInteractionCheck(
        Entity<SkillInteractionComponent?> ent,
        Entity<SkillsComponent?> user,
        EntityUid? target)
    {
        var targets = new List<EntityUid>();

        if (target != null)
            targets.Add(target.Value);

        return DoInteractionCheck(ent, user, targets);
    }

    /// <summary>
    /// Performs skill checks for interaction and, according to the result of the check,
    /// gives out experience and triggers interaction effects
    /// </summary>
    /// <param name="ent">Entity on which the interaction is performed</param>
    /// <param name="user">The user who performs the interaction</param>
    /// <param name="targets">Target entities of the interaction</param>
    /// <returns>Skills check result for interaction</returns>
    public SkillCheckResult DoInteractionCheck(
        Entity<SkillInteractionComponent?> ent,
        Entity<SkillsComponent?> user,
        List<EntityUid> targets)
    {
        if (!Resolve(ent, ref ent.Comp, false) || !Resolve(user, ref user.Comp, false))
            return SkillCheckResult.Success;

        var interact = ent.Comp;
        var delta = GetLevel(user, interact.Skill) - interact.TargetLevel;
        var successChance = interact.SuccessFactor * delta + 0.2f;
        var failChance = 0.4f - successChance;

        var fail = Random.Prob(Math.Clamp(failChance, 0, 1));
        var success = Random.Prob(Math.Clamp(successChance, 0, 1));

        if (success)
        {
            AddExperience(user, ent.Comp.Skill, (int)(interact.Experience * interact.ExpSuccessFactor));

            foreach (var effect in interact.SuccessEffects)
            {
                var args = new EntityEffectBaseArgs(ent, EntityManager);

                if (effect.ShouldApply(args, Random))
                    effect.Effect(args);
            }

            foreach (var effect in interact.SuccessUserEffects)
            {
                var args = new EntityEffectBaseArgs(ent, EntityManager);

                if (effect.ShouldApply(args, Random))
                    effect.Effect(args);
            }

            foreach (var effect in interact.SuccessTargetEffects)
            {
                foreach (var target in targets)
                {
                    var args = new EntityEffectBaseArgs(target, EntityManager);

                    if (effect.ShouldApply(args, Random))
                        effect.Effect(args);
                }
            }

#if DEBUG
            Sawmill.Debug($"user has passed the entity skill test with success. " +
                          $"User: {ToPrettyString(user)}, checker: {ToPrettyString(ent)}, " +
                          $"targets: {string.Join(", ", targets.Select(x => ToPrettyString(x)))}, " +
                          $"Checked skill: {Loc.GetString(Proto.Index(ent.Comp.Skill).Name)}");
#endif

            return SkillCheckResult.AdditionalSuccess;
        }

        if (fail)
        {
            AddExperience(user, ent.Comp.Skill, (int)(interact.Experience * interact.ExpFailFactor));

            foreach (var effect in interact.FailEffects)
            {
                var args = new EntityEffectBaseArgs(ent, EntityManager);

                if (effect.ShouldApply(args, Random))
                    effect.Effect(args);
            }

            foreach (var effect in interact.FailUserEffects)
            {
                var args = new EntityEffectBaseArgs(ent, EntityManager);

                if (effect.ShouldApply(args, Random))
                    effect.Effect(args);
            }

            foreach (var effect in interact.FailTargetEffects)
            {
                foreach (var target in targets)
                {
                    var args = new EntityEffectBaseArgs(target, EntityManager);

                    if (effect.ShouldApply(args, Random))
                        effect.Effect(args);
                }
            }

#if DEBUG
            Sawmill.Debug($"user failed the entity skill test. " +
                          $"User: {ToPrettyString(user)}, checker: {ToPrettyString(ent)}, " +
                          $"targets: {string.Join(", ", targets.Select(x => ToPrettyString(x)))}, " +
                          $"Checked skill: {Loc.GetString(Proto.Index(ent.Comp.Skill).Name)}");
#endif

            return SkillCheckResult.Fail;
        }

        AddExperience(user, ent.Comp.Skill, interact.Experience);
        return SkillCheckResult.Success;
    }

    #endregion
}

public enum SkillCheckResult : byte
{
    /// <summary>
    /// Interaction successfully completed with additional effects invoked
    /// </summary>
    AdditionalSuccess,

    /// <summary>
    /// Interaction successfully completed
    /// </summary>
    Success,

    /// <summary>
    /// The interaction failed with the invocation of additional effects
    /// </summary>
    Fail,
}

/// <summary>
/// Raised when the skill level of an entity changes
/// </summary>
[Serializable]
public sealed class SkillLevelChanged(int level, int oldLevel) : EntityEventArgs
{
    /// <summary>
    /// Current skill level
    /// </summary>
    public int Level { get; } = level;

    /// <summary>
    /// Previous skill level
    /// </summary>
    /// <remarks>
    /// It is not always equal to level - 1!
    /// </remarks>
    public int OldLevel { get; } = oldLevel;
}
