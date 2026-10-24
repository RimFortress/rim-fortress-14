using Content.Shared._RF.ColorHelper;
using Robust.Shared.Input;
using Content.Client._RF.UserInterface.Controls;
using Content.Client._RF.UserInterface;

namespace Content.Client._RF.NpcJobs.UI;

/// <summary>
/// The priority of a job: a square button that shows the number (nothing at the minimum), a click raises the priority,
/// a right click lowers it, and it goes round at the ends. The look is <c>.priority-button</c>
/// (<c>style/buttons/priority.suil</c>), the color of the number is between <see cref="MinColor"/> and
/// <see cref="MaxColor"/>.
/// </summary>
public sealed partial class NpcJobPriorityButton : RfButton
{
    /// <summary>
    /// Minimum priority value
    /// </summary>
    public int MinValue
    {
        get;
        set
        {
            field = value;
            Priority = Math.Clamp(Priority, value, MaxValue);
        }
    }

    /// <summary>
    /// Maximum priority value
    /// </summary>
    public int MaxValue
    {
        get;
        set
        {
            field = value;
            Priority = Math.Clamp(Priority, MinValue, value);
        }
    }

    public int Priority
    {
        get;
        set
        {
            var oldVal = field;

            if (value > MaxValue)
                field = MinValue;
            else if (value < MinValue)
                field = MaxValue;
            else
                field = value;

            if (value == oldVal)
                return;

            UpdateText();
            OnPriorityChanged?.Invoke(field);
        }
    }

    /// <summary>
    /// The color with which the text will be displayed at minimum priority
    /// </summary>
    public Color MinColor { get; set; }

    /// <summary>
    /// The color with which the text will be displayed at maximum priority
    /// </summary>
    public Color MaxColor { get; set; }

    public Color Color => ColorHelper.InterpolatedColor(MinColor, MaxColor, Priority, MinValue, MaxValue);

    /// <summary>
    /// Invoked every time the priority is changed
    /// </summary>
    public event Action<int>? OnPriorityChanged;

    public NpcJobPriorityButton()
    {
        InitializeComponent();

        EnableAllKeybinds = true;
        AddSuilStyleClass(SuilClass.PriorityButton);

        // The color of the number is the priority's, the styles leave it alone.
        SuilSetHard("color");

        OnPressed += args =>
        {
            if (args.Event.Function == EngineKeyFunctions.UIClick)
                Priority++;
            else if (args.Event.Function == EngineKeyFunctions.UIRightClick)
                Priority--;
        };
    }

    private void UpdateText()
    {
        if (Priority == MinValue)
        {
            ClearText();
            return;
        }

        SetPlainText(Priority.ToString());
        TextColor = Color;
    }
}
