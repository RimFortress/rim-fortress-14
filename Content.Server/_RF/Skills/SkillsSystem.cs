using Content.Server.Construction;
using Content.Shared._RF.Skills;
using Content.Shared._RF.Skills.Components;
using Robust.Shared.Serialization.Manager;

namespace Content.Server._RF.Skills;

public sealed partial class SkillsSystem : SharedSkillsSystem
{
    [Dependency] private ISerializationManager _serialization = default!;

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

    [SubscribeLocalEvent]
    private void OnConstructionChange(Entity<SkillInteractionComponent> ent, ref ConstructionChangeEntityEvent args)
    {
        if (!HasComp<SkillInteractionComponent>(args.New))
            return;

        var newComp = AddComp<SkillInteractionComponent>(args.New);
        _serialization.CopyTo(ent.Comp, ref newComp, notNullableOverride: true);
    }
}
