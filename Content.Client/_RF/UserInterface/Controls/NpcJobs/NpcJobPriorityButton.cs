using Content.Shared._RF.ColorHelper;
using Robust.Client.UserInterface;
using Robust.Shared.Input;

namespace Content.Client._RF.UserInterface.Controls.NpcJobs;

public sealed partial class NpcJobPriorityButton : RfBoxContainer
{
    private int _minValue;
    private int _maxValue;
    private int _priority;

    /// <summary>
    /// Minimum priority value
    /// </summary>
    public int MinValue
    {
        get => _minValue;
        set
        {
            _minValue = value;
            Priority = Math.Clamp(Priority, value, MaxValue);
        }
    }

    /// <summary>
    /// Maximum priority value
    /// </summary>
    public int MaxValue
    {
        get => _maxValue;
        set
        {
            _maxValue = value;
            Priority = Math.Clamp(Priority, MinValue, value);
        }
    }

    public int Priority
    {
        get => _priority;
        set
        {
            var oldVal = _priority;

            if (value > MaxValue)
                _priority = MinValue;
            else if (value < MinValue)
                _priority = MaxValue;
            else
                _priority = value;

            if (value == oldVal)
                return;

            Label.Visible = _priority != MinValue;
            Label.Text = _priority.ToString();
            Label.Modulate = Color;

            OnPriorityChanged?.Invoke(_priority);
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

    public Color Color =>
        ColorHelper.InterpolatedColor(MinColor, MaxColor, Priority, MinValue, MaxValue);

    /// <summary>
    /// Invoked every time the priority is changed
    /// </summary>
    public event Action<int>? OnPriorityChanged;

    public NpcJobPriorityButton()
    {
        InitializeComponent();
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        if (args.Function == EngineKeyFunctions.UIClick)
            Priority++;
        else if (args.Function == EngineKeyFunctions.UIRightClick)
            Priority--;
    }
}
