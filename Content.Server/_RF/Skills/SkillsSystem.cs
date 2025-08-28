using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.Construction;
using Content.Shared._RF.Skills;
using Content.Shared._RF.Skills.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization.Manager;

namespace Content.Server._RF.Skills;

public sealed class SkillsSystem : SharedSkillsSystem
{
    [Dependency] private readonly ISerializationManager _serialization = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SkillsComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<SkillInteractionComponent, ConstructionChangeEntityEvent>(OnConstructionChange);
    }

    private void OnInit(EntityUid uid, SkillsComponent component, ComponentInit args)
    {
        foreach (var data in component.Skills)
        {
            data.CurrentLevel = GetLevel(data.Id, data.CurrentExp);
            data.LevelUpExp = GetLevelMaxPoints(data.Id, data.CurrentLevel);
            data.MinLevelExp = GetLevelMinPoints(data.Id, data.CurrentLevel);
        }

        RandomizeSkills(
            new(uid, component),
            component.RandomizeSkills,
            component.RandomLevels.Next(Random),
            component.MaxRandomLevel);
    }

    private void OnConstructionChange(EntityUid uid,
        SkillInteractionComponent component,
        ConstructionChangeEntityEvent args)
    {
        var newComp = AddComp<SkillInteractionComponent>(args.New);
        _serialization.CopyTo(component, ref newComp, notNullableOverride: true);
    }

    /// <summary>
    /// Adds a skill for an entity
    /// </summary>
    public bool AddSkill(
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

    public void SetSkillLevel(Entity<SkillsComponent?> ent, ProtoId<SkillPrototype> skill, int level, bool dirty = true)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        if (!TryGetSkillData(ent, skill, out var data) && !AddSkill(ent, skill, out data, false))
            return;

        var exp = GetLevelMinPoints(skill, level) - data.CurrentExp;

        AddExperience(ent, skill, exp, dirty);
    }

    public void RandomizeSkills(Entity<SkillsComponent?> ent, List<ProtoId<SkillPrototype>> skills, int levels, int maxLevel)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        while (levels > 0 && skills.Count != 0)
        {
            var skill = Random.Pick(skills);

            if (!TryGetSkillData(ent, skill, out var data) && !AddSkill(ent, skill, out data, false))
                continue;

            var level = Math.Min(Random.Next(0, maxLevel - data.CurrentLevel), levels);

            levels -= level;
            level += data.CurrentLevel;

            if (level >= maxLevel)
                skills.Remove(skill);

            SetSkillLevel(ent, skill, level);
        }

        Dirty(ent);
    }
}
