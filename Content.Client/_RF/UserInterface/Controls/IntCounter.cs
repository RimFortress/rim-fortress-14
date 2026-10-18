namespace Content.Client._RF.UserInterface.Controls;

public sealed partial class IntCounter : RfBoxContainer
{
    private int _value;

    /// <summary>
    /// Current counter value
    /// </summary>
    public int Value
    {
        get => _value;
        set
        {
            var val = _value;
            _value = value;

            if (_value > MaxValue)
                _value = MaxValue.Value;

            if (_value < MinValue)
                _value = MinValue.Value;

            Minus.Disabled = _value == MinValue;
            Plus.Disabled = _value == MaxValue;
            CountLine.Text = _value.ToString();

            if (val == _value)
                return;

            OnValueChanged?.Invoke((val, _value));
        }
    }

    /// <summary>
    /// Maximum counter value
    /// </summary>
    public int? MaxValue
    {
        get;
        set
        {
            field = value;
            Value = _value;
        }
    }

    /// <summary>
    /// Minimum counter value
    /// </summary>
    public int? MinValue
    {
        get;
        set
        {
            field = value;
            Value = _value;
        }
    }

    public bool DisableIncrease
    {
        get => Plus.Disabled;
        set => Plus.Disabled = value;
    }

    public bool DisableDecrease
    {
        get => Minus.Disabled;
        set => Minus.Disabled = value;
    }

    /// <summary>
    /// Called whenever the counter value changes
    /// </summary>
    public event Action<(int Old, int New)>? OnValueChanged;

    public IntCounter()
    {
        InitializeComponent();

        CountLine.Text = Value.ToString();
    }

    private void OnCountTextChanged(RfLineEdit.LineEditEventArgs args)
        => Value = int.TryParse(args.Text, out var value) ? value : 0;

    /// <summary>
    /// Set counter value without invoking event
    /// </summary>
    public void SetValue(int value)
    {
        if (value > MaxValue)
            value = MaxValue.Value;

        if (value < MinValue)
            value = MinValue.Value;

        _value = value;
        Minus.Disabled = value == MinValue;
        Plus.Disabled = value == MaxValue;
        CountLine.Text = value.ToString();
    }
}

