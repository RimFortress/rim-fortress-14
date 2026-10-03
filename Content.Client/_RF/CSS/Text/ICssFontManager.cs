using Robust.Client.Graphics;

namespace Content.Client._RF.CSS.Text;

/// <summary>
/// Maps CSS font properties (<c>font-family</c>, <c>font-weight</c>, <c>font-style</c>, <c>font-size</c>)
/// to engine <see cref="Font"/> instances.
/// </summary>
public interface ICssFontManager
{
    /// <summary>
    /// Replaces the faces declared by <c>@font-face</c> rules of the stylesheet and drops all cached fonts.
    /// Faces added through <see cref="RegisterFace"/> and aliases are kept. On a conflict the face
    /// registered later wins; CSS faces come after the ones registered from code.
    /// </summary>
    /// <param name="faces">All faces declared by the stylesheet, in declaration order.</param>
    void SetFaces(IEnumerable<CssFontFace> faces);

    /// <summary>
    /// Registers a font face from code.
    /// </summary>
    void RegisterFace(CssFontFace face);

    /// <summary>
    /// Registers a generic family (<c>sans-serif</c>, <c>monospace</c>, ...) as an alias for real families.
    /// </summary>
    /// <param name="alias">The alias, e.g. <c>sans-serif</c>.</param>
    /// <param name="families">The real family names, in order of preference.</param>
    void RegisterAlias(string alias, params string[] families);

    /// <summary>
    /// Resolves a font for the given CSS properties.
    /// </summary>
    /// <param name="family">The <c>font-family</c> list. Every known family contributes a face, so glyphs missing
    /// in the first family fall back to the following ones.</param>
    /// <param name="weight">CSS <c>font-weight</c>.</param>
    /// <param name="slant">CSS <c>font-style</c>.</param>
    /// <param name="size">CSS <c>font-size</c> in virtual pixels.</param>
    /// <returns>The font; the theme default font if none of the families is registered.</returns>
    Font Resolve(CssFontFamily family, FontWeight weight, FontSlant slant, float size);
}
