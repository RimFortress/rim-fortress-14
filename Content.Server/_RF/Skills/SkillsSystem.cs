using System.Linq;
using Content.Shared._RF.Skills;
using Content.Shared._RF.Skills.Components;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._RF.Skills;

public sealed class SkillsSystem : SharedSkillsSystem
{
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SkillsComponent, ComponentInit>(OnInit);
    }

    private void OnInit(EntityUid uid, SkillsComponent component, ComponentInit args)
    {
        foreach (var data in component.Skills)
        {
            data.CurrentLevel = GetLevel(data.Id, data.CurrentExp);
            data.LevelUpExp = GetLevelMaxPoints(data.Id, data.CurrentLevel);
            data.MinLevelExp = GetLevelMinPoints(data.Id, data.CurrentLevel);
        }

        Dirty(uid, component);
    }

    /// <summary>
    /// Adds a skill for an entity
    /// </summary>
    public bool AddSkill(Entity<SkillsComponent?> ent, ProtoId<SkillPrototype> skill)
    {
        if (!Resolve(ent, ref ent.Comp)
            || ent.Comp.Skills.Any(x => x.Id == skill.Id)
            || !Proto.TryIndex(skill, out var proto))
            return false;

        var data = new SkillData
        {
            Id = skill,
            CurrentLevel = 0,
            CurrentExp = 0,
            LevelUpExp = proto.LevelUpExp,
            MinLevelExp = 0,
        };

        ent.Comp.Skills.Add(data);
        Dirty(ent);
        return true;
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
        if (!Resolve(ent, ref ent.Comp, false) || !Resolve(user, ref user.Comp, false))
            return SkillCheckResult.Success;

        var interact = ent.Comp;
        var delta = GetLevel(user, interact.Skill) - interact.TargetLevel;
        var fail = delta < 0 && _random.Prob(interact.FailCurve.Get(delta));
        var success = delta > 0 && _random.Prob(interact.SuccessCurve.Get(delta));

        if (success)
        {
            AddExperience(user, ent.Comp.Skill, (int)(interact.Experience * interact.ExpSuccessFactor));

            foreach (var effect in interact.SuccessEffects)
            {
                var args = new EntityEffectBaseArgs(ent, EntityManager);

                if (effect.ShouldApply(args, _random))
                    effect.Effect(args);
            }

            if (target == null)
                return SkillCheckResult.Fail;

            foreach (var effect in interact.SuccessTargetEffects)
            {
                var args = new EntityEffectBaseArgs(target.Value, EntityManager);

                if (effect.ShouldApply(args, _random))
                    effect.Effect(args);
            }

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

            if (target == null)
                return SkillCheckResult.Fail;

            foreach (var effect in interact.FailTargetEffects)
            {
                var args = new EntityEffectBaseArgs(target.Value, EntityManager);

                if (effect.ShouldApply(args, _random))
                    effect.Effect(args);
            }

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
