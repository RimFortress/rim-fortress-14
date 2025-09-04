namespace Content.Shared._RF.ColorHelper;

public static class ColorHelper
{
    /// <summary>
    /// Calculates the intermediate color between start and end based on the currentValue in the range [minValue, maxValue].
    /// </summary>
    public static Color InterpolatedColor(Color start, Color end, float currentValue, float minValue, float maxValue)
    {
        var t = (currentValue - minValue) / (maxValue - minValue);
        t = Math.Clamp(t, 0f, 1f);

        var r = start.R + (end.R - start.R) * t;
        var g = start.G + (end.G - start.G) * t;
        var b = start.B + (end.B - start.B) * t;

        return new Color(r, g, b);
    }
}
