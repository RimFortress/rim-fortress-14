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
}
