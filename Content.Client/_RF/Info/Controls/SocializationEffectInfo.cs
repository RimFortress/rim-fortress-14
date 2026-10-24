using Content.Client._RF.Suil;
using Content.Client._RF.UserInterface.Controls;
using Content.Client._RF.World;
using Content.Shared._RF.Social;
using Content.Shared.Localizations;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._RF.Info.Controls;

public sealed partial class SocializationEffectInfo : RfBoxContainer
{
    [Dependency] private IGameTiming _timing = default!;

    /// <summary>
    /// How often the time left is refreshed, in seconds
    /// </summary>
    private const float UpdateRate = 0.25f;

    public ProtoId<SocialEffectPrototype> Proto { get; private set; }
    public int Value { get; private set; }
    public TimeSpan? EndAt { get; private set; }

    public SocializationEffectInfo()
    {
        IoCManager.InjectDependencies(this);
        InitializeComponent();

        Scope.Every(UpdateRate, UpdateEndAt, runOnEnter: true);
    }

    public void UpdateInfo(SocialEffectPrototype proto, int value, TimeSpan? endAt)
    {
        Proto = proto;
        Value = value;
        EndAt = endAt;

        ToolTip = proto.Description != null ? Loc.GetString(proto.Description) : null;

        var color = value switch
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

        ValueLabel.Text = $"[b][color={color.ToHex()}]{sign}{Math.Abs(value)}[/color][/b]";
        Label.Text = Loc.GetString(proto.Name);
    }

    private void UpdateEndAt()
    {
        TimeSpan? duration = null;

        if (EndAt != null)
            duration = System<RimFortressWorldSystem>().ToWorldTime(EndAt.Value - _timing.CurTime);

        if (duration == null)
        {
            EndAtLabel.Text = Loc.GetString("effect-never-end");
            return;
        }

        var days = (int) Math.Floor(duration.Value.TotalDays);
        var timeLoc = ContentLocalizationManager.FormatTimeShort(duration.Value);

        EndAtLabel.Text = timeLoc;
        EndAtLabel.ToolTip = days > 0
            ? $"{timeLoc}, {duration.Value.Hours:00}:{duration.Value.Minutes:00}:{duration.Value.Seconds:00}"
            : $"{duration.Value.Hours:00}:{duration.Value.Minutes:00}:{duration.Value.Seconds:00}";
    }
}
