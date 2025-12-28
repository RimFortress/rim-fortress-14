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

        return Color.InterpolateBetween(start, end, t);
    }
}
