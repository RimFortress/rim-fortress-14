using Content.Server.Construction;
using Content.Server.Construction.Components;
using Content.Shared._RF.Skills;
using Content.Shared._RF.Skills.Components;

namespace Content.Server._RF.Skills;

public sealed partial class SkillsSystem : SharedSkillsSystem
{
    [SubscribeLocalEvent]
    private void OnInit(Entity<SkillsComponent> ent, ref ComponentInit args)
    {
        foreach (var data in ent.Comp.Skills)
        {
            data.CurrentLevel = GetLevel(data.Id, data.CurrentExp);
            data.LevelUpExp = GetLevelMaxPoints(data.Id, data.CurrentLevel);
            data.MinLevelExp = GetLevelMinPoints(data.Id, data.CurrentLevel);
            // Do not call Dirty, as this will be done in RandomizeSkills
        }

        RandomizeSkills(
            ent.AsNullable(),
            ent.Comp.RandomizeSkills,
            ent.Comp.RandomLevels.Next(Random),
            ent.Comp.MaxRandomLevel);
    }

    /// <summary>
    /// A construction becomes another entity at every step (the ghost, the frame, the next frame...), and the difficulty
    /// of the construction goes with it: otherwise the steps that follow the first one would not be a skill matter.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnConstructionChange(EntityUid uid, SkillDifficultyComponent component, ConstructionChangeEntityEvent args)
    {
        // The event is raised at the new entity too
        if (uid != args.Old || !HasComp<ConstructionComponent>(args.New))
            return;

        var difficulty = EnsureComp<SkillDifficultyComponent>(args.New);

        foreach (var (query, value) in component.Difficulty)
        {
            difficulty.Difficulty[query] = value;
        }
    }
}
