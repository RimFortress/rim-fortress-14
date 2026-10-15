using Content.Suil;

namespace Content.Client._RF.Suil;

/// <summary>
/// The SUIL-wide keywords: values accepted by every property.
/// </summary>
public enum SuilWideKeyword : byte
{
    /// <summary>
    /// Take the computed value of the parent (the nearest <see cref="UiElement"/> ancestor).
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
/// The value stored in a compiled property when its declaration is a SUIL-wide keyword. It is resolved
/// against the control when the style is applied, because the result depends on the control's parent.
/// </summary>
public sealed class SuilWideValue
{
    private static readonly SuilWideValue[] Instances =
    [
        new(SuilWideKeyword.Inherit),
        new(SuilWideKeyword.Initial),
        new(SuilWideKeyword.Unset),
        new(SuilWideKeyword.Revert),
    ];

    /// <summary>
    /// The keyword.
    /// </summary>
    public SuilWideKeyword Keyword { get; }

    private SuilWideValue(SuilWideKeyword keyword) => Keyword = keyword;

    /// <summary>
    /// Returns the shared value for a keyword.
    /// </summary>
    public static SuilWideValue For(SuilWideKeyword keyword) => Instances[(int) keyword];
}


/// <summary>
/// Recognizes SUIL-wide keywords in a declaration value.
/// </summary>
public static class SuilWideKeywordExtensions
{
    extension(SuilWideKeyword)
    {
        /// <summary>
        /// Checks whether the whole remaining stream is a single SUIL-wide keyword (case-insensitive).
        /// The stream is left untouched if it is not.
        /// </summary>
        /// <param name="stream">SUIL values stream.</param>
        /// <param name="keyword">The keyword.</param>
        public static bool TryParse(ref SuilValueStream stream, out SuilWideKeyword keyword)
        {
            keyword = default;

            if (stream.Peek() is not SuilIdent ident)
                return false;

            var mark = stream.Mark();

            if (!SuilWideKeyword.TryGet(ident.Value, out keyword))
                return false;

            stream.ConsumeNext();

            if (stream.AtEnd)
                return true;

            stream.Reset(mark);
            return false;
        }

        private static bool TryGet(string text, out SuilWideKeyword keyword)
        {
            switch (text.ToLowerInvariant())
            {
                case "inherit":
                    keyword = SuilWideKeyword.Inherit;
                    return true;
                case "initial":
                    keyword = SuilWideKeyword.Initial;
                    return true;
                case "unset":
                    keyword = SuilWideKeyword.Unset;
                    return true;
                case "revert":
                    keyword = SuilWideKeyword.Revert;
                    return true;
                default:
                    keyword = default;
                    return false;
            }
        }
    }
}
