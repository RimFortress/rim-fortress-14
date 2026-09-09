using System.Numerics;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client._RF.UserInterface.Controls;

public sealed class SimpleProgress : Control
{
    public float GetAsRatio() => (Value - MinValue) / (MaxValue - MinValue);

    public void SetAsRatio(float value)
    {
        Value = ClampValue(value * (MaxValue - MinValue) + MinValue);
    }

    [ViewVariables]
    public float MaxValue
    {
        get;
        set
        {
            field = value;
            _ensureValueClamped();
        }
    }

    [ViewVariables]
    public float MinValue
    {
        get;
        set
        {
            field = value;
            _ensureValueClamped();
        }
    }

    [ViewVariables]
    public float Value
    {
        get;
        set => field = ClampValue(value);
    }

    [ViewVariables]
    public Color Color { get; set; }

    private void _ensureValueClamped()
    {
        var newValue = ClampValue(Value);
        if (!MathHelper.CloseToPercent(newValue, Value))
        {
            Value = newValue;
        }
    }

    [Pure]
    private float ClampValue(float value)
        => MathHelper.Clamp(value, MinValue, MaxValue);

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        handle.DrawRect(
            UIBox2.FromDimensions(Vector2.Zero, new Vector2(PixelWidth * GetAsRatio(), PixelHeight)),
            Color);
    }
}
