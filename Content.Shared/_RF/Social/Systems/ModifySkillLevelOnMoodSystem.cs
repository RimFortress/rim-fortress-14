using Content.Shared._RF.Skills;
using Content.Shared._RF.Social.Components;

namespace Content.Shared._RF.Social.Systems;

public sealed class ModifySkillLevelOnMoodSystem : EntitySystem
{
    [Dependency] private readonly SocialSystem _social = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<ModifySkillLevelOnMoodComponent, GetSkillLevelModifierEvent>(OnGetSkillLevelModifier);
    }

    private void OnGetSkillLevelModifier(
        Entity<ModifySkillLevelOnMoodComponent> ent,
        ref GetSkillLevelModifierEvent args)
    {
        if (ent.Comp.Modifiers.TryGetValue(args.Skill, out var factor))
            args.Multiplier *= 1 + factor * _social.GetMood(ent.Owner);
    }
}
