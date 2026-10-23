using System.Numerics;
using System.Text;
using Robust.Client.Graphics;

namespace Content.Client._RF.Graphics;

/// <summary>
/// Text for the overlays and the controls that draw by themselves.
/// </summary>
public static class DrawingHandleTextExt
{
    /// <summary>
    /// The thickness of the outline of <see cref="DrawStringOutlined"/>, in pixels.
    /// </summary>
    public const float DefaultOutlineThickness = 1.5f;

    /// <summary>
    /// Draws a string with an outline, to be read over a map or the world: the same as
    /// <c>DrawString(font, position, text, color)</c> (the position is the top left corner), but the outline goes first.
    /// </summary>
    /// <param name="outline">The outline, black and <see cref="DefaultOutlineThickness"/> thick by default.</param>
    public static void DrawStringOutlined(
        this DrawingHandleScreen handle,
        Font font,
        Vector2 position,
        string text,
        Color color,
        float scale = 1f,
        TextOutline? outline = null)
    {
        var line = outline ?? new TextOutline(DefaultOutlineThickness, Color.Black);

        DrawPass(handle, font, position, text, scale, color, line);
        DrawPass(handle, font, position, text, scale, color, null);
    }

    private static void DrawPass(
        DrawingHandleScreen handle,
        Font font,
        Vector2 position,
        string text,
        float scale,
        Color color,
        TextOutline? outline)
    {
        var baseline = new Vector2(position.X, position.Y + font.GetAscent(scale));

        foreach (var rune in text.EnumerateRunes())
        {
            if (rune == new Rune('\n'))
            {
                baseline = new Vector2(position.X, baseline.Y + font.GetLineHeight(scale));
                continue;
            }

            var advance = outline is { } line
                ? font.DrawCharOutline(handle, rune, baseline, scale, line)
                : font.DrawChar(handle, rune, baseline, scale, color);

            baseline += new Vector2(advance, 0);
        }
    }
}
