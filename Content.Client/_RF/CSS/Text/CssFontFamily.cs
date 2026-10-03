using Robust.Client.Graphics;
using Robust.Shared.Utility;

namespace Content.Client._RF.CSS;

/// <summary>
/// A parsed CSS <c>font-family</c> list, in order of preference.
/// </summary>
public sealed class CssFontFamily
{
    /// <summary>
    /// The default family list: <c>sans-serif</c>.
    /// </summary>
    public static readonly CssFontFamily SansSerif = new(["sans-serif"]);

    /// <summary>
    /// Family names (or generic family aliases), in order of preference.
    /// </summary>
    public IReadOnlyList<string> Names { get; }

    /// <summary>
    /// A normalized string that identifies the list (used as a cache key).
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Creates a family list.
    /// </summary>
    public CssFontFamily(IReadOnlyList<string> names)
    {
        Names = names;
        Key = string.Join(',', names).ToLowerInvariant();
    }
}

/// <summary>
/// A single registered font face.
/// </summary>
/// <param name="Family">Family name.</param>
/// <param name="Weight">CSS weight, 1-1000.</param>
/// <param name="Slant">Font slant.</param>
/// <param name="Path">Resource path of the font file.</param>
public sealed record CssFontFace(string Family, int Weight, FontSlant Slant, ResPath Path);
