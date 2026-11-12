using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared._RF.Skills.Components;
using Content.Shared._RF.Skills.Prototypes;
using Content.Shared.EntityEffects;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization;

namespace Content.Shared._RF.Skills;

/// <summary>
/// Manages <see cref="SkillsComponent"/>
/// </summary>
public abstract partial class SharedSkillsSystem : EntitySystem
{
    [Dependency] protected IPrototypeManager Proto = null!;
    [Dependency] protected IRobustRandom Random = null!;
    [Dependency] private SharedEntityEffectsSystem _entityEffects = null!;

    public const string DefaultSkillProfession = "skill-profession-default";

    [SubscribeLocalEvent]
    private static void OnGetExpModifier(Entity<SkillExpModificatorComponent> ent, ref GetSkillExpModifierEvent ev)
    {
        if (ent.Comp.FlatModificators.TryGetValue(ev.Skill, out var modificator))
            ev.Modificator += modificator;

        if (ent.Comp.Multipliers.TryGetValue(ev.Skill, out var multiplier))
            ev.Multiplier *= multiplier;
    }

    /// <summary>
    /// Returns the profession name of the entity with the highest skill level
    /// </summary>
    /// <remarks>
    /// Returns the default profession name if no matching one is found
    /// </remarks>
    [PublicAPI, Pure]
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

    [PublicAPI, Pure]
    public string SkillProfession(IReadOnlyDictionary<ProtoId<SkillPrototype>, int> skills)
    {
        var defaultProf = Loc.GetString(DefaultSkillProfession);

        (ProtoId<SkillPrototype> Skill, int Level)? bestSkill = null;

        foreach (var (protoId, level) in skills)
        {
            if (level == 0
                || !Proto.TryIndex(protoId, out var proto)
                || proto.Profession == null)
                continue;

            if (bestSkill != null && bestSkill.Value.Level >= level)
                continue;

            bestSkill = (proto, level);
        }

        if (bestSkill == null)
            return defaultProf;

        return SkillProfession(bestSkill.Value.Skill, bestSkill.Value.Level) ?? defaultProf;
    }

    /// <summary>
    /// Returns the profession name for the given skill according to the current level
    /// </summary>
    [PublicAPI, Pure]
    public string? SkillProfession(Entity<SkillsComponent?> ent, ProtoId<SkillPrototype> skill)
        => SkillProfession(skill, GetLevel(ent, skill));

    /// <summary>
    /// Returns the profession name for the given skill according to the current level
    /// </summary>
    [PublicAPI, Pure]
    public string? SkillProfession(ProtoId<SkillPrototype> skill, int level)
    {
        if (!Proto.TryIndex(skill, out var proto) || proto.Profession == null)
            return null;

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
    /// Returns the maximum level of the given skill.
    /// </summary>
    /// <param name="skill">Skill prototype.</param>
    [PublicAPI, Pure]
    public int GetMaxLevel(ProtoId<SkillPrototype> skill)
        => Proto.Resolve(skill, out var proto) ? proto.MaxLevel : 0;

    /// <summary>
    /// Returns the current skill level of the entity
    /// </summary>
    [PublicAPI, Pure]
    public int GetLevel(Entity<SkillsComponent?> ent, ProtoId<SkillPrototype> skill)
    {
        if (!Resolve(ent, ref ent.Comp, false)
            || ent.Comp.Skills.FirstOrDefault(x => x.Id == skill) is not { } data)
            return 0;

        return data.CurrentLevel;
    }

    /// <summary>
    /// Returns the skill level of the entity using all valid modifiers
    /// </summary>
    [PublicAPI, Pure]
    public int GetModifiedLevel(Entity<SkillsComponent?> ent, ProtoId<SkillPrototype> skill)
    {
        if (!Proto.TryIndex(skill, out var proto))
            return 0;

        var ev = new GetSkillLevelModifierEvent(skill, 0, 1f);
        RaiseLocalEvent(ent, ref ev);

        return Math.Clamp((int)((GetLevel(ent, skill) + ev.Modificator) * ev.Multiplier), 0, proto.MaxLevel);
    }

    /// <summary>
    /// Calculates the skill level for a given amount of experience
    /// </summary>
    [PublicAPI, Pure]
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

        return Math.Min((int)Math.Floor(level), proto.MaxLevel);
    }

    /// <summary>
    /// Returns the maximum amount of experience for the given skill level
    /// </summary>
    [PublicAPI, Pure]
    public int GetLevelMaxPoints(ProtoId<SkillPrototype> skill, int level)
    {
        if (!Proto.TryIndex(skill, out var proto))
            return 0;

        level = Math.Clamp(level, 0, proto.MaxLevel) + 1;

        // If I hadn't skipped school I wouldn't have had
        // to search for the sum of geometric progression formula
        // Sₙ = a₁ * (qⁿ - 1) / (q - 1)
        return (int)(proto.LevelUpExp
                      * (Math.Pow(proto.LevelExpMultiplier, level) - 1)
                      / (proto.LevelExpMultiplier - 1));
    }

    /// <summary>
    /// Returns the minimum amount of experience for the given skill level
    /// </summary>
    [PublicAPI, Pure]
    public int GetLevelMinPoints(ProtoId<SkillPrototype> skill, int level)
        => level <= 0 ? 0 : GetLevelMaxPoints(skill, level - 1);

    [PublicAPI, Pure]
    public bool TryGetSkillData(
        Entity<SkillsComponent?> ent,
        ProtoId<SkillPrototype> skill,
        [NotNullWhen(true)] out SkillData? data)
    {
        data = null;

        if (!Resolve(ent, ref ent.Comp, false)
            || ent.Comp.Skills.FirstOrDefault(x => x.Id == skill) is not { } skillData)
            return false;

        data = skillData;
        return true;
    }

    /// <summary>
    /// Adds experience points for the skill of the given entity.
    /// </summary>
    [PublicAPI]
    public void AddExperience(Entity<SkillsComponent?> ent, SkillExperience skill)
        => AddExperience(ent, skill.Skill, skill.Exp);

    /// <summary>
    /// Adds experience points for the skill of the given entity.
    /// </summary>
    [PublicAPI]
    public void AddExperience(Entity<SkillsComponent?> ent, ProtoId<SkillPrototype> skill, int amount, bool dirty = true)
    {
        if (!Resolve(ent, ref ent.Comp, false)
            || !Proto.TryIndex(skill, out var proto)
            || amount == 0)
            return;

        if (!TryGetSkillData(ent, skill, out var data) && !AddSkill(ent, skill, out data))
            return;

        var oldLevel = data.CurrentLevel;

        data.CurrentExp += amount;

        if (data.CurrentExp > data.LevelUpExp && data.CurrentLevel == proto.MaxLevel)
            data.CurrentExp = data.LevelUpExp;

        data.CurrentLevel = GetLevel(data.Id, data.CurrentExp);
        data.LevelUpExp = GetLevelMaxPoints(data.Id, data.CurrentLevel);
        data.MinLevelExp = GetLevelMinPoints(data.Id, data.CurrentLevel);

        if (dirty)
            Dirty(ent);

        if (oldLevel == data.CurrentLevel)
            return;

        if (proto.LevelUpEffects.TryGetValue(0, out var zeroEffects))
            _entityEffects.ApplyEffects(ent, zeroEffects);

        if (data.CurrentLevel != 0 && proto.LevelUpEffects.TryGetValue(data.CurrentLevel, out var effects))
            _entityEffects.ApplyEffects(ent, effects);

        RaiseLocalEvent(ent, new SkillLevelChanged(data.CurrentLevel, oldLevel));
    }

    /// <summary>
    /// Adds a skill for an entity
    /// </summary>
    [PublicAPI]
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

    /// <summary>
    /// Sets the skill level of an entity
    /// </summary>
    [PublicAPI]
    public void SetSkillLevel(Entity<SkillsComponent?> ent, ProtoId<SkillPrototype> skill, int level, bool dirty = true)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        if (!TryGetSkillData(ent, skill, out var data) && !AddSkill(ent, skill, out data, false))
            return;

        var exp = GetLevelMinPoints(skill, level) - data.CurrentExp;

        if (level != 0)
            exp++;

        AddExperience(ent, skill, exp, dirty);
    }

    /// <summary>
    /// Randomizes the skill level of the entity
    /// </summary>
    /// <param name="ent">Entity</param>
    /// <param name="skills">Skills that need to be randomized</param>
    /// <param name="levels">How many levels will be randomly given out for these skills</param>
    /// <param name="maxLevel">Maximum level that can be given by randomization</param>
    /// <remarks>
    /// If <paramref name="levels"/> more than <paramref name="maxLevel"/> * <paramref name="skills"/>.Count
    /// then in total the entity will gain <paramref name="maxLevel"/> * <paramref name="skills"/>.Count skill levels
    /// </remarks>
    [PublicAPI]
    public void RandomizeSkills(Entity<SkillsComponent?> ent, List<ProtoId<SkillPrototype>> skills, int levels, int maxLevel)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        foreach (var skill in skills)
        {
            SetSkillLevel(ent, skill, 0, false);
        }

        while (levels > 0 && skills.Count != 0)
        {
            var skill = Random.Pick(skills);

            if (!TryGetSkillData(ent, skill, out var data) && !AddSkill(ent, skill, out data, false))
                continue;

            var level = Math.Min(Random.Next(1, maxLevel - data.CurrentLevel), levels);

            levels -= level;
            level += data.CurrentLevel;

            if (level >= maxLevel)
                skills.Remove(skill);

            SetSkillLevel(ent, skill, level, false);
        }

        Dirty(ent);
    }
}

[Serializable, NetSerializable]
public enum SkillCheckTarget : byte
{
    /// <summary>
    /// The entity that initiated the action (hit another entity, picked it up, etc.).
    /// </summary>
    User,

    /// <summary>
    /// An entity that serves as an intermediary between the <see cref="User"/>
    /// and the <see cref="Target"/> — most often a tool in the user’s hand.
    /// </summary>
    Used,

    /// <summary>
    /// The target of <see cref="User"/> interaction.
    /// </summary>
    Target,
}

[Serializable, NetSerializable]
public enum SkillCheckResult : byte
{
    /// <summary>
    /// Interaction successfully completed with additional effects invoked.
    /// </summary>
    AdditionalSuccess,

    /// <summary>
    /// Interaction successfully completed.
    /// </summary>
    Success,

    /// <summary>
    /// The interaction failed with the invocation of additional effects.
    /// </summary>
    Fail,
}

public static class SkillCheckResultExtensions
{
    extension(SkillCheckResult result)
    {
        public bool IsFail => result == SkillCheckResult.Fail;
    }
}

/// <summary>
/// Raised when the skill level of an entity changes
/// </summary>
/// <param name="Level">Current skill level.</param>
/// <param name="OldLevel">Previous skill level. It is not always equal to Level - 1.</param>
[PublicAPI]
public record struct SkillLevelChanged(int Level, int OldLevel);

/// <summary>
/// Event called to get information about experience modifiers for a particular skill
/// </summary>
/// <param name="Uid">Entity for which an experience modifier should be given</param>
[ByRefEvent]
public record struct GetSkillExpModifierEvent(EntityUid Uid, ProtoId<SkillPrototype> Skill, int Modificator, float Multiplier);

/// <summary>
/// Event called to get a modifier for an entity's skill level
/// </summary>
/// <param name="Skill">Modifiers for which skill to get</param>
/// <param name="Modificator">Flat modifier that will be added to the skill level</param>
/// <param name="Multiplier">Modifier that the skill level will be multiplied by after adding the flat modifier</param>
[ByRefEvent]
public record struct GetSkillLevelModifierEvent(ProtoId<SkillPrototype> Skill, int Modificator, float Multiplier);
