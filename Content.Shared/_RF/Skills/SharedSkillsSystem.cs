using System.Linq;
using Content.Shared._RF.Skills.Components;
using Content.Shared.DoAfter;
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
    [Dependency] private readonly IRobustRandom _random = default!;

    protected ISawmill Sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();

        Sawmill = LogManager.GetSawmill("skills");
    }

    public int GetLevel(Entity<SkillsComponent?> ent, ProtoId<SkillPrototype> skill)
    {
        if (!Resolve(ent, ref ent.Comp)
            || ent.Comp.Skills.FirstOrDefault(x => x.Id == skill) is not { } data)
            return 0;

        return data.CurrentLevel;
    }

    public int GetLevel(ProtoId<SkillPrototype> skill, int experience)
    {
        if (!Proto.TryIndex(skill, out var proto)
            || proto.LevelExpMultiplier <= 0)
            return 0;

        // When LevelExpMultiplier == 1
        if (Math.Abs(proto.LevelExpMultiplier - 1.0) < 1e-10)
            return experience / proto.LevelUpExp;

        var level = Math.Log(
            experience * (proto.LevelExpMultiplier - 1) / proto.LevelUpExp + 1,
            proto.LevelExpMultiplier);

        return Math.Min((int) Math.Floor(level), proto.MaxLevel);
    }

    public int GetLevelMaxPoints(ProtoId<SkillPrototype> skill, int level)
    {
        if (!Proto.TryIndex(skill, out var proto))
            return 0;

        if (level <= 0)
            return proto.LevelUpExp;

        // If I hadn't skipped school I wouldn't have had
        // to search for the sum of geometric progression formula
        return (int) (proto.LevelUpExp
                      * (Math.Pow(proto.LevelExpMultiplier, level) - 1)
                      / (proto.LevelExpMultiplier - 1));
    }

    public int GetLevelMinPoints(ProtoId<SkillPrototype> skill, int level)
    {
        return level <= 0 ? 0 : GetLevelMaxPoints(skill, level - 1);
    }

    /// <summary>
    /// Adds experience points for the skill of the given entity
    /// </summary>
    public void AddExperience(Entity<SkillsComponent?> ent, ProtoId<SkillPrototype> skill, int amount)
    {
        if (!Resolve(ent, ref ent.Comp, false)
            || !Proto.TryIndex(skill, out var proto)
            || ent.Comp.Skills.FirstOrDefault(x => x.Id == skill.Id) is not { } data)
            return;

        var oldLevel = data.CurrentLevel;

        data.CurrentExp += (int) (amount * data.ExpFactor * ent.Comp.ExpFactor);

        if (data.CurrentExp > data.LevelUpExp && data.CurrentLevel == proto.MaxLevel)
            data.CurrentExp = data.LevelUpExp;

        if (data.CurrentExp >= data.MinLevelExp && data.CurrentExp <= data.LevelUpExp)
        {
            Dirty(ent);
            return;
        }

        data.CurrentLevel = GetLevel(data.Id, data.CurrentExp);
        data.LevelUpExp = GetLevelMaxPoints(data.Id, data.CurrentLevel);
        data.MinLevelExp = GetLevelMinPoints(data.Id, data.CurrentLevel);
        Dirty(ent);

        var args = new EntityEffectBaseArgs(ent, EntityManager);

        if (proto.LevelUpEffects.TryGetValue(0, out var zeroEffects))
        {
            foreach (var effect in zeroEffects)
            {
                if (effect.ShouldApply(args, _random))
                    effect.Effect(args);
            }
        }

        if (data.CurrentLevel != 0 && proto.LevelUpEffects.TryGetValue(data.CurrentLevel, out var effects))
        {
            foreach (var effect in effects)
            {
                if (effect.ShouldApply(args, _random))
                    effect.Effect(args);
            }
        }

        RaiseLocalEvent(ent, new SkillLevelChanged(data.CurrentLevel, oldLevel));
    }

    #region Interactions

    /// <summary>
    /// Changes some input number depending on the result of the skill check for the interaction
    /// </summary>
    public int GetInteractionResult(Entity<SkillInteractionComponent?> ent, Entity<SkillsComponent?> user, int value)
    {
        return (int) Math.Floor(GetInteractionResult(ent, user, (float) value));
    }

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

    public TimeSpan GetDelay(Entity<SkillInteractionComponent?> ent,
        Entity<SkillsComponent?> user,
        TimeSpan delay)
    {
        return TimeSpan.FromSeconds(GetDelay(ent, user, (float) delay.TotalSeconds));
    }

    public float GetDelay(Entity<SkillInteractionComponent?> ent, Entity<SkillsComponent?> user, float delay)
    {
        if (!Resolve(ent, ref ent.Comp) || !Resolve(user, ref user.Comp))
            return delay;

        var delta = GetLevel(user, ent.Comp.Skill) - ent.Comp.TargetLevel;

        return Math.Clamp(delay - delta * ent.Comp.DoAfterFactor,
            ent.Comp.MinDoAfterTime,
            ent.Comp.MaxDoAfterTime);
    }

    public SkillCheckResult DoInteractionCheck(Entity<SkillInteractionComponent?> ent, DoAfterEvent args)
    {
        return DoInteractionCheck(ent, args.User, args.Target);
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

        var fail = _random.Prob(failChance);
        var success = _random.Prob(successChance);

        if (success)
        {
            AddExperience(user, ent.Comp.Skill, (int)(interact.Experience * interact.ExpSuccessFactor));

            foreach (var effect in interact.SuccessEffects)
            {
                var args = new EntityEffectBaseArgs(ent, EntityManager);

                if (effect.ShouldApply(args, _random))
                    effect.Effect(args);
            }

            foreach (var effect in interact.SuccessUserEffects)
            {
                var args = new EntityEffectBaseArgs(ent, EntityManager);

                if (effect.ShouldApply(args, _random))
                    effect.Effect(args);
            }

            foreach (var effect in interact.SuccessTargetEffects)
            {
                foreach (var target in targets)
                {
                    var args = new EntityEffectBaseArgs(target, EntityManager);

                    if (effect.ShouldApply(args, _random))
                        effect.Effect(args);
                }
            }

#if DEBUG
            Sawmill.Debug($"user has passed the entity skill test with success. " +
                          $"User: {ToPrettyString(user)}, checker: {ToPrettyString(ent)}, " +
                          $"targets: {targets.Select(x => ToPrettyString(x))}" +
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

                if (effect.ShouldApply(args, _random))
                    effect.Effect(args);
            }

            foreach (var effect in interact.FailUserEffects)
            {
                var args = new EntityEffectBaseArgs(ent, EntityManager);

                if (effect.ShouldApply(args, _random))
                    effect.Effect(args);
            }

            foreach (var effect in interact.FailTargetEffects)
            {
                foreach (var target in targets)
                {
                    var args = new EntityEffectBaseArgs(target, EntityManager);

                    if (effect.ShouldApply(args, _random))
                        effect.Effect(args);
                }
            }

#if DEBUG
            Sawmill.Debug($"user failed the entity skill test. " +
                          $"User: {ToPrettyString(user)}, checker: {ToPrettyString(ent)}, " +
                          $"targets: {targets.Select(x => ToPrettyString(x))}" +
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
