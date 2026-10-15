using Content.Client._RF.Suil.Text;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Shared.Utility;

namespace Content.Client._RF.Suil.Markup;

/// <summary>
/// <c>[b]</c>: bold text.
/// </summary>
[UsedImplicitly]
public sealed class RfBoldTag : IRfMarkupTagHandler
{
    /// <inheritdoc/>
    public string Name => "b";

    /// <inheritdoc/>
    public void Push(MarkupNode node, RfMarkupContext context)
        => context.Push(context.Current with { Weight = FontWeight.Bold });
}

/// <summary>
/// <c>[i]</c>: italic text.
/// </summary>
[UsedImplicitly]
public sealed class RfItalicTag : IRfMarkupTagHandler
{
    /// <inheritdoc/>
    public string Name => "i";

    /// <inheritdoc/>
    public void Push(MarkupNode node, RfMarkupContext context)
        => context.Push(context.Current with { Slant = FontSlant.Italic });
}

/// <summary>
/// <c>[bi]</c>: bold italic text.
/// </summary>
[UsedImplicitly]
public sealed class RfBoldItalicTag : IRfMarkupTagHandler
{
    /// <inheritdoc/>
    public string Name => "bi";

    /// <inheritdoc/>
    public void Push(MarkupNode node, RfMarkupContext context)
        => context.Push(context.Current with { Weight = FontWeight.Bold, Slant = FontSlant.Italic });
}

/// <summary>
/// <c>[u]</c>: underlined text.
/// </summary>
[UsedImplicitly]
public sealed class RfUnderlineTag : IRfMarkupTagHandler
{
    /// <inheritdoc/>
    public string Name => "u";

    /// <inheritdoc/>
    public void Push(MarkupNode node, RfMarkupContext context)
        => context.Push(context.Current with { Decoration = context.Current.Decoration | SuilTextDecorationLine.Underline });
}

/// <summary>
/// <c>[s]</c>: struck-through text.
/// </summary>
[UsedImplicitly]
public sealed class RfStrikeTag : IRfMarkupTagHandler
{
    /// <inheritdoc/>
    public string Name => "s";

    /// <inheritdoc/>
    public void Push(MarkupNode node, RfMarkupContext context)
        => context.Push(context.Current with { Decoration = context.Current.Decoration | SuilTextDecorationLine.LineThrough });
}

/// <summary>
/// <c>[color=red]</c> / <c>[color=#ff0000]</c>: colored text. An unknown color leaves the text unchanged.
/// </summary>
[UsedImplicitly]
public sealed class RfColorTag : IRfMarkupTagHandler
{
    /// <inheritdoc/>
    public string Name => "color";

    /// <inheritdoc/>
    public void Push(MarkupNode node, RfMarkupContext context)
    {
        if (node.Value.TryGetColor(out var color))
            context.Push(context.Current with { Color = color.Value });
    }
}

/// <summary>
/// <c>[font="Noto Sans, Arial" size=14 weight=700 style=italic]</c>: changes the font family list, size,
/// weight and slant. The parameter is a comma-separated <c>font-family</c> list resolved through
/// <see cref="ISuilFontManager"/>, not an engine font prototype id. Every part is optional and
/// defaults to the surrounding text's value.
/// </summary>
[UsedImplicitly]
public sealed class RfFontTag : IRfMarkupTagHandler
{
    /// <inheritdoc/>
    public string Name => "font";

    /// <inheritdoc/>
    public void Push(MarkupNode node, RfMarkupContext context)
    {
        var style = context.Current;

        if (node.Value.StringValue is { } family)
        {
            var names = family.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            if (names.Length > 0)
                style = style with { Family = new SuilFontFamily(names) };
        }

        if (node.Attributes.TryGetValue("size", out var size) && size.LongValue is { } sizeValue and > 0)
            style = style with { Size = sizeValue };

        if (node.Attributes.TryGetValue("weight", out var weight))
        {
            if (weight.LongValue is { } weightValue and >= 1 and <= 1000)
                style = style with { Weight = (FontWeight) (ushort) weightValue };
            else if (string.Equals(weight.StringValue, "bold", StringComparison.OrdinalIgnoreCase))
                style = style with { Weight = FontWeight.Bold };
            else if (string.Equals(weight.StringValue, "normal", StringComparison.OrdinalIgnoreCase))
                style = style with { Weight = FontWeight.Normal };
        }

        if (node.Attributes.TryGetValue("style", out var slant)
            && slant.StringValue is { } slantText
            && Enum.TryParse<FontSlant>(slantText, true, out var parsedSlant))
            style = style with { Slant = parsedSlant };

        context.Push(style);
    }
}

/// <summary>
/// <c>[head=1]</c>: a heading. The level (1-3, anything else is a minor heading) scales the surrounding
/// font size; the text is bold and followed by a line break.
/// </summary>
[UsedImplicitly]
public sealed class RfHeadingTag : IRfMarkupTagHandler
{
    private static readonly float[] Scales = [2f, 1.5f, 1.25f];
    private const float MinorScale = 1.1f;

    /// <inheritdoc/>
    public string Name => "head";

    /// <inheritdoc/>
    public void Push(MarkupNode node, RfMarkupContext context)
    {
        var level = node.Value.LongValue ?? 1;
        var scale = level >= 1 && level <= Scales.Length ? Scales[level - 1] : MinorScale;
        var current = context.Current;

        context.Push(current with { Size = MathF.Round(current.Size * scale), Weight = FontWeight.Bold });
    }

    /// <inheritdoc/>
    public string TextAfter(MarkupNode node) => "\n";
}

/// <summary>
/// <c>[bullet]</c>: an indented bullet point before the following text.
/// </summary>
[UsedImplicitly]
public sealed class RfBulletTag : IRfMarkupTagHandler
{
    /// <inheritdoc/>
    public string Name => "bullet";

    /// <inheritdoc/>
    public string TextBefore(MarkupNode node) => "  \u2022 ";
}
