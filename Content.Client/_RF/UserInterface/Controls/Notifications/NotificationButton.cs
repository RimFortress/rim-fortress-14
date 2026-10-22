using Content.Client._RF.Notifications;
using Content.Client._RF.Suil;
using Content.Shared._RF.Notifications;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._RF.UserInterface.Controls.Notifications;

public sealed partial class NotificationButton : RfBoxContainer
{
    [Dependency] private IEntityManager _entity = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IGameTiming _timing = default!;

    private NotificationsSystem? _notifications;

    private readonly Notification? _notification;
    private bool _setup;
    private const float TimeUpdateRate = 1f;

    public bool Pressed
    {
        get => Button.Pressed;
        set
        {
            Button.Pressed = value;
            DescContainer.Visible = value;
        }
    }

    public event Action<RfButtonEventArgs>? OnPressed;

    public NotificationButton(Notification notification)
    {
        IoCManager.InjectDependencies(this);
        InitializeComponent();

        _notification = notification;

        Button.OnPressed += OnButtonPressed;
        FocusButton.OnPressed += _ => OnFocusPressed();

        Button.OnKeyBindUp += args =>
        {
            if (args.Function != EngineKeyFunctions.UseSecondary)
                return;

            if (_player.LocalEntity is not { } uid
                || !_proto.TryIndex(_notification?.ProtoId, out var proto))
                return;

            _notifications?.RemoveNotification(uid, proto);
        };

        Button.TooltipSupplier = SupplyTooltip;
        FocusButton.Visible = notification.TargetCoords != null || notification.Target != null;

        Scope.EveryFrame(_ => UpdateProgress());
        Scope.Every(TimeUpdateRate, UpdateTime, runOnEnter: true);

        if (_notification.ExpireAt == null)
            return;

        ProgressBar.Visible = true;
        ProgressBar.BackgroundColor = Color.White.WithAlpha(0.1f);
        ProgressBar.SuilSetHard("background-color");
    }

    private void OnButtonPressed(RfButtonEventArgs args)
    {
        Pressed = args.Button.Pressed;
        OnPressed?.Invoke(args);
    }

    private void OnFocusPressed()
    {
        if (_notification != null && _notifications != null && _player.LocalEntity is { } uid)
            _notifications.FocusToNotification(uid, _notification);
    }

    public void EnsureSetup()
    {
        if (_setup || _notification == null)
            return;

        _setup = true;
        _notifications = _entity.System<NotificationsSystem>();

        var proto = _proto.Index(_notification.ProtoId);

        NameLabel.SetPlainText(Loc.GetString(proto.NameId));
        Desc.Text = _notification.Description;

        if (_notification.Duplications > 0)
            NameLabel.Text = $"{NameLabel.Text} x{_notification.Duplications + 1}";

        if (proto.Style != null)
            Button.AddSuilStyleClass(proto.Style);
    }

    private void UpdateProgress()
    {
        if (_notification?.ExpireAt is not { } expireAt)
            return;

        var fill = 1f - (float)((expireAt - _timing.CurTime) / (expireAt - _notification.StartedAt));
        ProgressBar.SetWidth = Button.Width * Math.Clamp(fill, 0f, 1f);
    }

    private void UpdateTime()
    {
        if (_notification == null)
            return;

        TimeLabel.Text = Loc.GetString("notification-start-time",
            ("time", _timing.CurTime - _notification.StartedAt));
    }

    private Control SupplyTooltip(Control? sender)
    {
        var msg = FormattedMessage.FromMarkupOrThrow(
            Button.ToolTip = $"{NameLabel.Text}\n{Loc.GetString("notification-name-tooltip")}");
        var tooltip = new Robust.Client.UserInterface.CustomControls.Tooltip();
        tooltip.SetMessage(msg);
        return tooltip;
    }
}
