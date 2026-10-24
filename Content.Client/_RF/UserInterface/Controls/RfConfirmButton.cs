using Content.Suil;
using Content.Client._RF.Suil;
using JetBrains.Annotations;
using Robust.Shared.Timing;

namespace Content.Client._RF.UserInterface.Controls;

/// <summary>
/// A button that requires a second click to actually invoke <see cref="OnPressed"/>, the <see cref="RfButton"/>
/// version of <see cref="Content.Client.UserInterface.Controls.ConfirmButton"/>.
/// </summary>
/// <remarks>
/// <para>
/// After the first click the button shows <see cref="ConfirmationText"/> and gets the
/// <see cref="SuilClass.Confirming"/> style class, until the second click or <see cref="ResetTime"/>. The second click is
/// ignored for <see cref="CooldownTime"/> to avoid a double click confirming it.
/// </para>
/// <para>
/// The colors of the confirming state are in the stylesheet (<c>button.confirming</c>).
/// </para>
/// </remarks>
[Virtual, UsedImplicitly]
[SuilTag("confirm-button")]
public partial class RfConfirmButton : RfButton
{
    [Dependency] private IGameTiming _gameTiming = null!;

    private TimeSpan? _nextReset;
    private TimeSpan? _nextCooldown;
    private string? _confirmationText;
    private string? _text;
    private bool _isConfirming;

    /// <summary>
    /// Fired when the button was pressed and confirmed.
    /// </summary>
    public new event Action<RfButtonEventArgs>? OnPressed;

    /// <summary>
    /// Fired when the button has started to confirm and is awaiting a second press.
    /// </summary>
    public event Action<RfButtonEventArgs>? OnConfirming;

    /// <summary>
    /// The text of the button while it isn't confirming.
    /// </summary>
    public new string? Text
    {
        get => _text;
        set
        {
            _text = value;
            UpdateText();
        }
    }

    /// <summary>
    /// Overrides the confirming state of the button when set.
    /// </summary>
    public new bool Disabled
    {
        get => base.Disabled;
        set
        {
            SetIsConfirming(false);
            base.Disabled = value;
        }
    }

    /// <summary>
    /// The text displayed on the button when waiting for a second click.
    /// </summary>
    public string ConfirmationText
    {
        get => _confirmationText ?? Loc.GetString("generic-confirm");
        set
        {
            _confirmationText = value;
            UpdateText();
        }
    }

    /// <summary>
    /// The time until the button reverts to normal.
    /// </summary>
    public TimeSpan ResetTime { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// The time until the button accepts a second click.
    /// </summary>
    public TimeSpan CooldownTime { get; set; } = TimeSpan.FromSeconds(.5);

    /// <summary>
    /// Is the button awaiting a second press?
    /// </summary>
    public bool IsConfirming
    {
        get => _isConfirming;
        set
        {
            if (_isConfirming != value)
                SetIsConfirming(value);
        }
    }

    public RfConfirmButton()
    {
        base.OnPressed += HandleOnPressed;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (!IsConfirming)
            return;

        if (_gameTiming.CurTime > _nextReset)
            SetIsConfirming(false);
        else if (base.Disabled && _gameTiming.CurTime > _nextCooldown)
            base.Disabled = false;
    }

    private void HandleOnPressed(RfButtonEventArgs args)
    {
        // Prevent accidental confirmations from double clicking.
        if (IsConfirming && _nextCooldown > _gameTiming.CurTime)
            return;

        SetIsConfirming(!IsConfirming);

        if (IsConfirming)
            OnConfirming?.Invoke(args);
        else
            OnPressed?.Invoke(args);
    }

    private void UpdateText()
    {
        base.Text = IsConfirming ? ConfirmationText : Text;
    }

    private void SetIsConfirming(bool value)
    {
        _isConfirming = value;

        UpdateText();

        if (value)
        {
            AddSuilStyleClass(SuilClass.Confirming);

            // Disable the button until the cooldown ends.
            base.Disabled = true;
            _nextCooldown = _gameTiming.CurTime + CooldownTime;
            _nextReset = _gameTiming.CurTime + ResetTime;
        }
        else
        {
            RemoveSuilStyleClass(SuilClass.Confirming);

            // Enable the button if the previous SetIsConfirming(true) disabled it.
            if (base.Disabled && _nextReset != null)
                base.Disabled = false;

            _nextReset = null;
            _nextCooldown = null;
        }
    }
}
