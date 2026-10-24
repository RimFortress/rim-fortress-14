using Content.Client._RF.Suil;
using Content.Shared._RF.Social;
using Content.Shared._RF.Social.Systems;
using Content.Client._RF.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client._RF.Info.Controls;

public sealed partial class OpinionInfoButton : RfBoxContainer
{
    [Dependency] private IEntityManager _entity = default!;

    public int Value { get; private set; }
    public Dictionary<ProtoId<SocialEffectPrototype>, TimeSpan?> Effects { get; private set; } = new();

    public OpinionInfoButton()
    {
        IoCManager.InjectDependencies(this);
        InitializeComponent();

        Button.OnToggled += _ =>
        {
            if (Effects.Count > 0)
                EffectsPanel.Visible = Button.Pressed;
        };
    }

    public void UpdateInfo(
        EntityUid uid,
        EntityUid targetUid,
        Dictionary<ProtoId<SocialEffectPrototype>, TimeSpan?> effects)
    {
        Effects = effects;
        Value = System<SocialSystem>().GetOpinion(uid, targetUid);

        Button.ToggleMode = Effects.Count > 0;

        var name = _entity.GetComponent<MetaDataComponent>(targetUid).EntityName;

        var color = Value switch
        {
            < 0 => SuilPalette.LightBad,
            > 0 => SuilPalette.Good,
            _ => SuilPalette.GraySilver,
        };

        var sign = Value switch
        {
            > 0 => "+",
            < 0 => "–",
            _ => "",
        };

        Sprite.SetEntity(targetUid);
        Label.Text = $"[bold]{name}[/bold]";
        ValueLabel.Text = $"[bold][color={color.ToHex()}]{sign}{Math.Abs(Value)}[/color][/bold]";
        EffectsContainer.UpdateInfo(effects);
    }
}

