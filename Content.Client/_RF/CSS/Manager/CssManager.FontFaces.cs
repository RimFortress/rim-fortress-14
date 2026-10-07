using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Client._RF.CSS.Parsers;
using Content.Client._RF.CSS.Streams;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Utility;

namespace Content.Client._RF.CSS;

// The @font-face at-rule: binds a TTF/OTF file to a font-family name, weight and style.
public sealed partial class CssManager
{
    /// <summary>
    /// Font faces declared by <c>@font-face</c> rules, per file.
    /// </summary>
    private readonly Dictionary<ResPath, List<CssFontFace>> _fontFacesByFile = new();

    /// <summary>
    /// All faces declared by <c>@font-face</c> rules, ordered by file path (later declarations win).
    /// </summary>
    private List<CssFontFace> GetFontFaces() => _fontFacesByFile
        .OrderBy(kv => kv.Key.ToString(), StringComparer.Ordinal)
        .SelectMany(kv => kv.Value)
        .ToList();

    /// <summary>
    /// Parses the descriptors of a <c>@font-face</c> rule: <c>font-family</c> (one name), <c>src</c>
    /// (the first <c>url()</c>, a <see cref="ResPath"/> of a TTF/OTF file; <c>format()</c> and <c>local()</c>
    /// are ignored), <c>font-weight</c> and <c>font-style</c>. Other descriptors are ignored with a warning.
    /// <c>var()</c> is not supported inside <c>@font-face</c>.
    /// </summary>
    private bool TryParseFontFace(CssAtRule rule, [NotNullWhen(true)] out CssFontFace? face)
    {
        face = null;

        if (rule.Block is not { } block)
            return FontFaceError("@font-face has no block");

        string? family = null;
        ResPath? source = null;
        var weight = (int) FontWeight.Normal;
        var slant = FontSlant.Normal;

        foreach (var item in CssParser.ParseListOfDeclarations(block.Value.ToArray()))
        {
            if (item is not CssDeclaration declaration)
                continue;

            var stream = new CssValueStream(declaration.Value);

            switch (declaration.Name.ToLowerInvariant())
            {
                case "font-family":
                    if (!TryParseValue<CssFontFamily>(ref stream, out var parsedFamily)
                        || parsedFamily is not { Names.Count: 1 }
                        || !stream.AtEnd)
                        return FontFaceError("@font-face: font-family must be a single family name");

                    family = parsedFamily.Names[0];
                    break;
                case "font-weight":
                    if (!_customParsers[typeof(CssFontWeightParser)].TryParse(ref stream, out var parsedWeight)
                        || !stream.AtEnd)
                    {
                        return FontFaceError(
                            "@font-face: font-weight must be normal, bold or a number (ranges are not supported)");
                    }

                    weight = (int) (FontWeight) parsedWeight;
                    break;
                case "font-style":
                    if (!TryParseValue<FontSlant>(ref stream, out var parsedSlant) || !stream.AtEnd)
                        return FontFaceError("@font-face: font-style must be normal, italic or oblique");

                    slant = parsedSlant;
                    break;
                case "src":
                    if (!TryGetFontSource(declaration.Value, out source))
                        return FontFaceError("@font-face: src must contain url(\"/Path/To/Font.ttf\")");

                    break;
                default:
                    _sawmill.Warning($"@font-face: descriptor '{declaration.Name}' is not supported and was ignored");
                    break;
            }
        }

        if (family == null)
            return FontFaceError("@font-face: font-family is missing");

        if (source == null)
            return FontFaceError("@font-face: src is missing");

        face = new CssFontFace(family, weight, slant, source.Value);
        return true;
    }

    /// <summary>
    /// Finds the first <c>url()</c> in a <c>src</c> value and turns it into a rooted <see cref="ResPath"/>.
    /// </summary>
    private static bool TryGetFontSource(IReadOnlyList<ICssComponentValue> value, [NotNullWhen(true)] out ResPath? path)
    {
        foreach (var item in value)
        {
            switch (item)
            {
                // url(/Fonts/A.ttf) without quotes is tokenized as a single url token.
                case UrlToken url:
                    path = ToRooted(url.Value);
                    return true;
                // url("/Fonts/A.ttf") is a function that holds a string.
                case CssFunction func when func.Name.Equals("url", StringComparison.OrdinalIgnoreCase):
                    foreach (var argument in func.Value)
                    {
                        if (argument is not StringToken text)
                            continue;

                        path = ToRooted(text.Value);
                        return true;
                    }

                    break;
            }
        }

        path = null;
        return false;

        static ResPath ToRooted(string raw)
        {
            var parsed = new ResPath(raw);
            return parsed.IsRooted ? parsed : parsed.ToRootedPath();
        }
    }

    private bool FontFaceError(string message)
    {
#if EXCEPTION_TOLERANCE
        _sawmill.Error(message);
        return false;
#else
        throw new ArgumentException(message);
#endif
    }

    /// <summary>
    /// Drops the cached text styles of the control and its descendants after the font faces were replaced.
    /// </summary>
    private static void RefreshFonts(Control control)
    {
        if (control is CssControl css)
            css.RefreshFonts();

        foreach (var child in control.Children)
        {
            RefreshFonts(child);
        }
    }
}
