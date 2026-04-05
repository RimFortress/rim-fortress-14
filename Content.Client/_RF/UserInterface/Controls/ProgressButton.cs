using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._RF.UserInterface.Controls;

[UsedImplicitly, Virtual]
public class ProgressButton : Button
{
    public const string StylePropertyForeground = "foreground";

    private StyleBox? _foregroundStyleBoxOverride;
    private float _maxValue = 100;
    private float _minValue;
    private float _value;
    private bool _rounded;
    private int _roundingDecimals;

    public event Action<ProgressButton>? OnValueChanged;

    public float GetAsRatio() => (_value - _minValue) / (_maxValue - _minValue);

    public void SetAsRatio(float value)
    {
        Value = ClampValue(value * (_maxValue - _minValue) + _minValue);
    }

    [ViewVariables]
    public float MaxValue
    {
        get => _maxValue;
        set
        {
            _maxValue = value;
            _ensureValueClamped();
        }
    }

    [ViewVariables]
    public float MinValue
    {
        get => _minValue;
        set
        {
            _minValue = value;
            _ensureValueClamped();
        }
    }

    [ViewVariables]
    public float Value
    {
        get => _value;
        set
        {
            var newValue = ClampValue(value);
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (newValue != _value)
            {
                _value = newValue;
                OnValueChanged?.Invoke(this);
            }
        }
    }

    [ViewVariables]
    public bool Rounded
    {
        get => _rounded;
        set
        {
            _rounded = value;
            _ensureValueClamped();
        }
    }

    [ViewVariables]
    public int RoundingDecimals
    {
        get => _roundingDecimals;
        set
        {
            _roundingDecimals = value;
            _ensureValueClamped();
        }
    }

    public void SetValueWithoutEvent(float newValue)
    {
        newValue = ClampValue(newValue);
        _value = newValue;
    }

    private void _ensureValueClamped()
    {
        var newValue = ClampValue(_value);
        if (!MathHelper.CloseToPercent(newValue, _value))
        {
            _value = newValue;
            OnValueChanged?.Invoke(this);
        }
    }

    [System.Diagnostics.Contracts.Pure]
    private float ClampValue(float value)
    {
        if (_rounded)
        {
            value = MathF.Round(value, _roundingDecimals);
        }
        return MathHelper.Clamp(value, _minValue, _maxValue);
    }

    public StyleBox? ForegroundStyleBoxOverride
    {
        get => _foregroundStyleBoxOverride;
        set
        {
            _foregroundStyleBoxOverride = value;
            InvalidateMeasure();
        }
    }

    [System.Diagnostics.Contracts.Pure]
    private StyleBox? GetForeground()
        => ForegroundStyleBoxOverride
           ?? (TryGetStyleProperty<StyleBox>(StylePropertyForeground, out var ret) ? ret : null);

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        if (GetForeground() is not { } fg)
            return;

        var minSize = fg.MinimumSize;
        var size = PixelWidth * GetAsRatio() - minSize.X;

        if (size > 0)
        {
            fg.Draw(handle,
                UIBox2.FromDimensions(PixelSizeBox.Left, PixelSizeBox.Top, minSize.X + size, PixelSizeBox.Height),
                UIScale);
        }
    }
}
