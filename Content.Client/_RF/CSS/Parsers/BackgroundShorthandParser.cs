using System.Diagnostics.CodeAnalysis;
using Content.Client._RF.Graphics;
using JetBrains.Annotations;
using Robust.Client.Graphics;

namespace Content.Client._RF.CSS.Parsers;

[UsedImplicitly]
public sealed partial class BackgroundShorthandParser : ICssShorthandParser
{
    [Dependency] private ICssManager _css = default!;

    public string CssName => "background";

    public IReadOnlyList<string> Longhands { get; } =
    [
        "background-color", "background-image", "background-repeat", "background-size", "background-position",
    ];

    public bool TryExpand(ref CssValueStream stream, [NotNullWhen(true)] out object?[]? values)
    {
        values = null;

        Color? color = null;
        Texture? image = null;
        RfStyleBoxTexture.StretchMode? repeat = null;
        RfStyleBoxTexture.StyleBoxSize? size = null;
        RfStyleBoxTexture.StyleBoxPosition? position = null;
        var sawPosition = false;

        while (!stream.AtEnd)
        {
            if (stream.Peek() is CssFunction { Name: "texture" })
            {
                if (!_css.TryParseValue<Texture>(ref stream, out var v))
                    return false;

                image = v;
                continue;
            }

            if (stream.Peek() is IdentToken { Value: "none" })
            {
                stream.ConsumeNext();
                continue;
            }

            if (stream.Peek() is IdentToken { Value: "repeat" or "repeat-x" or "repeat-y" or "no-repeat" })
            {
                if (!_css.TryParseValue<RfStyleBoxTexture.StretchMode>(ref stream, out var v))
                    return false;
                repeat = v;
                continue;
            }

            if (!sawPosition && LooksLikePositionStart(stream))
            {
                if (!_css.TryParseValue<RfStyleBoxTexture.StyleBoxPosition>(ref stream, out var v))
                    return false;

                position = v;
                sawPosition = true;

                if (stream.Peek() is DelimToken { CodePoint: '/' })
                {
                    stream.ConsumeNext();

                    if (!_css.TryParseValue<RfStyleBoxTexture.StyleBoxSize>(ref stream, out var s))
                        return false;

                    size = s;
                }

                continue;
            }

            if (!_css.TryParseValue<Color>(ref stream, out var c))
                return false;

            color = c;
        }

        values =
        [
            (Color?)(color ?? Color.Transparent),
            image,
            repeat ?? RfStyleBoxTexture.StretchMode.Repeat,
            size ?? RfStyleBoxTexture.StyleBoxSize.Auto,
            position ?? RfStyleBoxTexture.StyleBoxPosition.Default,
        ];
        return true;
    }

    private static bool LooksLikePositionStart(CssValueStream stream) => stream.Peek() is
        IdentToken { Value: "left" or "right" or "top" or "bottom" or "center" }
        or PercentageToken
        or DimensionToken
        or NumberToken { Value: 0 };
}
