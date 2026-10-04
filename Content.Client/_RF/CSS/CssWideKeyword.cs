using Content.Client._RF.CSS.Streams;

namespace Content.Client._RF.CSS;

/// <summary>
/// The CSS-wide keywords: values accepted by every property.
/// </summary>
public enum CssWideKeyword : byte
{
    /// <summary>
    /// Take the computed value of the parent (the nearest <see cref="CssControl"/> ancestor).
    /// </summary>
    Inherit,

    /// <summary>
    /// Take the initial value of the property (its default).
    /// </summary>
    Initial,

    /// <summary>
    /// <see cref="Inherit"/> for inherited properties, <see cref="Initial"/> for the rest.
    /// </summary>
    Unset,

    /// <summary>
    /// Roll back to the value from the previous origin. There are no user-agent or user origins here,
    /// so it behaves as <see cref="Unset"/>.
    /// </summary>
    Revert,
}

/// <summary>
/// The value stored in a compiled property when its declaration is a CSS-wide keyword. It is resolved
/// against the control when the style is applied, because the result depends on the control's parent.
/// </summary>
public sealed class CssWideValue
{
    private static readonly CssWideValue[] Instances =
    [
        new(CssWideKeyword.Inherit),
        new(CssWideKeyword.Initial),
        new(CssWideKeyword.Unset),
        new(CssWideKeyword.Revert),
    ];

    /// <summary>
    /// The keyword.
    /// </summary>
    public CssWideKeyword Keyword { get; }

    private CssWideValue(CssWideKeyword keyword) => Keyword = keyword;

    /// <summary>
    /// Returns the shared value for a keyword.
    /// </summary>
    public static CssWideValue For(CssWideKeyword keyword) => Instances[(int) keyword];
}


/// <summary>
/// Recognizes CSS-wide keywords in a declaration value.
/// </summary>
public static class CssWideKeywordExtensions
{
    extension(CssWideKeyword)
    {
        /// <summary>
        /// Checks whether the whole remaining stream is a single CSS-wide keyword (case-insensitive).
        /// The stream is left untouched if it is not.
        /// </summary>
        /// <param name="stream">CSS values stream.</param>
        /// <param name="keyword">The keyword.</param>
        public static bool TryParse(ref CssValueStream stream, out CssWideKeyword keyword)
        {
            keyword = default;

            if (stream.Peek() is not IdentToken ident)
                return false;

            var mark = stream.Mark();

            if (!CssWideKeyword.TryGet(ident.Value, out keyword))
                return false;

            stream.ConsumeNext();

            if (stream.AtEnd)
                return true;

            stream.Reset(mark);
            return false;
        }

        private static bool TryGet(string text, out CssWideKeyword keyword)
        {
            switch (text.ToLowerInvariant())
            {
                case "inherit":
                    keyword = CssWideKeyword.Inherit;
                    return true;
                case "initial":
                    keyword = CssWideKeyword.Initial;
                    return true;
                case "unset":
                    keyword = CssWideKeyword.Unset;
                    return true;
                case "revert":
                    keyword = CssWideKeyword.Revert;
                    return true;
                default:
                    keyword = default;
                    return false;
            }
        }
    }
}
