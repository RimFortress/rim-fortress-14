using Content.Client._RF.Suil;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Client._RF.Suil.Parsers;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Utility;

namespace Content.Suil;

// The @font-face at-rule: binds a TTF/OTF file to a font-family name, weight and style.
public sealed partial class SuilManager
{
    /// <summary>
    /// Font faces declared by <c>@font-face</c> rules, per file.
    /// </summary>
    private readonly Dictionary<ResPath, List<SuilFontFaceEntry>> _fontFacesByFile = new();

    /// <summary>
    /// All faces declared by <c>@font-face</c> rules, ordered by file path (later declarations win).
    /// </summary>
    private List<SuilFontFaceEntry> GetFontFaces() => _fontFacesByFile
        .OrderBy(kv => kv.Key.ToString(), StringComparer.Ordinal)
        .SelectMany(kv => kv.Value)
        .ToList();

    /// <summary>
    /// Parses the descriptors of a <c>@font-face</c> rule: <c>font-family</c> (one name), <c>src</c>
    /// (the first <c>url()</c>, a <see cref="ResPath"/> of a TTF/OTF file; <c>format()</c> and <c>local()</c>
    /// are ignored), <c>font-weight</c> and <c>font-style</c>. Other descriptors are ignored with a warning.
    /// Variables (<c>&lt;name&gt;</c>) are expanded in <c>@font-face</c> like everywhere else.
    /// </summary>
    private bool TryParseFontFace(IReadOnlyList<SuilDeclaration> descriptors, [NotNullWhen(true)] out SuilFontFaceEntry? face)
    {
        face = null;

        string? family = null;
        ResPath? source = null;
        var weight = (int) FontWeight.Normal;
        var slant = FontSlant.Normal;

        foreach (var declaration in descriptors)
        {
            var stream = new SuilValueStream(declaration.Value);

            switch (declaration.Name.ToLowerInvariant())
            {
                case "font-family":
                    if (!_values.TryParse<SuilFontFamily>(ref stream, out var parsedFamily)
                        || parsedFamily is not { Names.Count: 1 }
                        || !stream.AtEnd)
                        return FontFaceError("@font-face: font-family must be a single family name");

                    family = parsedFamily.Names[0];
                    break;
                case "font-weight":
                    if (!_values.TryParseWith(typeof(SuilFontWeightParser), ref stream, out var parsedWeight)
                        || !stream.AtEnd)
                    {
                        return FontFaceError(
                            "@font-face: font-weight must be normal, bold or a number (ranges are not supported)");
                    }

                    weight = (int) (FontWeight) parsedWeight;
                    break;
                case "font-style":
                    if (!_values.TryParse<FontSlant>(ref stream, out var parsedSlant) || !stream.AtEnd)
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

        face = new SuilFontFaceEntry(family, weight, slant, source.Value);
        return true;
    }

    /// <summary>
    /// Finds the first <c>url()</c> in a <c>src</c> value and turns it into a rooted <see cref="ResPath"/>.
    /// </summary>
    private static bool TryGetFontSource(IReadOnlyList<ISuilValue> value, [NotNullWhen(true)] out ResPath? path)
    {
        foreach (var item in value)
        {
            // url("/Fonts/A.ttf") is a function that holds a string.
            if (item is not SuilFunction func || !func.Name.Equals("url", StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (var argument in func.Arguments.SelectMany(a => a))
            {
                if (argument is not SuilString text)
                    continue;

                path = ToRooted(text.Text);
                return true;
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
        var error = new SuilStyleError("reading a font face", message, _at);
#if EXCEPTION_TOLERANCE
        _sawmill.Error(error.Message);
        return false;
#else
        throw error;
#endif
    }

    /// <summary>
    /// Drops the cached text styles of the control and its descendants after the font faces were replaced.
    /// </summary>
    private static void RefreshFonts(Control control)
    {
        if (control is UiElement suil)
            suil.RefreshFonts();

        foreach (var child in control.Children)
        {
            RefreshFonts(child);
        }
    }
}
