using System.Linq;
using Content.Shared._RF.Skills.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Skills;

/// <summary>
/// Manages <see cref="SkillsComponent"/>
/// </summary>
public abstract class SharedSkillsSystem : EntitySystem
{
    [Dependency] protected readonly IPrototypeManager Proto = default!;

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
            > 0 => value * delta * ent.Comp.ResultIncreaseFactor,
            < 0 => value * delta * ent.Comp.ResultDecreaseFactor,
        };

        return Math.Clamp(result, ent.Comp.MinResult, ent.Comp.MaxResult);
    }

    public float GetDoAfterDelay(Entity<SkillInteractionComponent?> ent, Entity<SkillsComponent?> user, float delay)
    {
        if (!Resolve(ent, ref ent.Comp) || !Resolve(user, ref user.Comp))
            return delay;

        var delta = GetLevel(user, ent.Comp.Skill) - ent.Comp.TargetLevel;

        return Math.Clamp(delay - delay * delta * ent.Comp.DoAfterFactor,
            ent.Comp.MinDoAfterTime,
            ent.Comp.MaxDoAfterTime);
    }

    #endregion
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
