using Content.Client._RF.Skills;
using Content.Client._RF.UserInterface.Controls;
using Content.Shared._RF.Skills;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client._RF.Lobby.UI;

public sealed partial class CharacterSkill : RfBoxContainer
{
    [Dependency] private IEntityManager _entity = default!;
    [Dependency] private IPrototypeManager _prototype = default!;

    public CharacterSkill(ProtoId<SkillPrototype> skill, int level, float? modifier = null)
    {
        IoCManager.InjectDependencies(this);
        InitializeComponent();

        if (!_prototype.TryIndex(skill, out var proto))
            return;

        var skillSys = _entity.System<SkillsSystem>();
        var sprite = _entity.System<SpriteSystem>();

        Texture.Texture = proto.Icon != null ? sprite.Frame0(proto.Icon) : null;
        Texture.ModulateSelfOverride = proto.Color;

        CostLabel.Text = $"{Loc.GetString("character-skills-tab-points")}: {proto.LevelPreferencesPoints}";

        Levels.MinValue = 0;
        Levels.MaxValue = proto.MaxPreferencesLevel;
        Levels.Value = level;
        Levels.OnValueChanged += value => UpdateName(proto, value.New, skillSys, modifier);

        UpdateName(proto, level, skillSys, modifier);
    }

    private void UpdateName(SkillPrototype skill, int level, SkillsSystem skillSys, float? modifier = null)
    {
        var text = skillSys.SkillProfession(skill, level);

        if (modifier != null)
            text = $"{text} [color={skill.Color.ToHex()}]({(int)(modifier.Value * 100)}%)[/color]";

        NameLabel.Text = text;
    }

}

