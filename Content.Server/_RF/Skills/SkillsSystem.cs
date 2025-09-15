using Content.Server.Construction;
using Content.Shared._RF.Skills;
using Content.Shared._RF.Skills.Components;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization.Manager;

namespace Content.Server._RF.Skills;

public sealed partial class SkillsSystem : SharedSkillsSystem
{
    [Dependency] private readonly ISerializationManager _serialization = default!;
    [Dependency] private readonly IConsoleHost _host = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SkillsComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<SkillInteractionComponent, ConstructionChangeEntityEvent>(OnConstructionChange);

        InitializeCommands();
    }

    private void OnInit(EntityUid uid, SkillsComponent component, ComponentInit args)
    {
        foreach (var data in component.Skills)
        {
            data.CurrentLevel = GetLevel(data.Id, data.CurrentExp);
            data.LevelUpExp = GetLevelMaxPoints(data.Id, data.CurrentLevel);
            data.MinLevelExp = GetLevelMinPoints(data.Id, data.CurrentLevel);
            // Do not call Dirty, as this will be done in RandomizeSkills
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
        if (!HasComp<SkillInteractionComponent>(args.New))
            return;

        var newComp = AddComp<SkillInteractionComponent>(args.New);
        _serialization.CopyTo(component, ref newComp, notNullableOverride: true);
    }

    /// <summary>
    /// Sets the skill level of an entity
    /// </summary>
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
