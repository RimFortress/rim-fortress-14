using System.Diagnostics.CodeAnalysis;
using Content.Shared._RF.Utilities;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Reflection;

namespace Content.Client._RF.CSS;

public static partial class CssParser
{
    /// <summary>
    /// Parses the selector from <see cref="CssQualifiedRule"/>.
    /// </summary>
    /// <param name="rule"></param>
    /// <param name="reflection">Reflection manager. Used to get the <see cref="SelectorElement.ElementType"/>.</param>
    /// <param name="selectors">A selectors extracted from a rule.</param>
    /// <returns>True if the selector has been successfully parsed.</returns>
    /// <exception cref="CssParserError">
    /// If !EXCEPTION_TOLERANCE and the <paramref name="rule"/> contains syntax errors in the selector.
    /// </exception>
    [PublicAPI, Pure]
    public static bool TryGetSelectors(
        CssQualifiedRule rule,
        IReflectionManager reflection,
        [NotNullWhen(true)] out List<Selector>? selectors)
        => TryGetSelectors(rule.Prelude, reflection, out selectors);

    private static bool TryGetSelectors(
        List<ICssComponentValue> prelude,
        IReflectionManager reflection,
        [NotNullWhen(true)] out List<Selector>? selectors)
    {
        selectors = new List<Selector>();

        foreach (var part in SplitByComma(prelude))
        {
            if (!TryGetSelectorFromPrelude(part, reflection, out var selector))
            {
                selectors = null;
                return false;
            }

            selectors.Add(selector);
        }

        return true;
    }


    /// <summary>
    /// Parses one selector group member — everything between two commas (or the
    /// whole prelude, if there's only one). Handles the '>' combinator and compound-selector parsing.
    /// </summary>
    private static bool TryGetSelectorFromPrelude(
        List<ICssComponentValue> prelude,
        IReflectionManager reflection,
        [NotNullWhen(true)] out Selector? selector)
    {
        selector = null;

        if (!TrySplitCombinatorSegments(prelude, out var segments))
        {
#if EXCEPTION_TOLERANCE
            return false;
#else
            throw new CssParserError("resolving selector",
                "expected compound selectors joined only by '>' (descendant combinator via whitespace is not supported)");
#endif
        }

        var compounds = new List<Selector>(segments.Count);

        foreach (var segment in segments)
        {
            if (!TryParseCompoundSelector(segment, reflection, out var compound))
                return false; // inner call already threw when !EXCEPTION_TOLERANCE

            compounds.Add(compound);
        }

        if (compounds.Count == 1)
        {
            selector = compounds[0];
            return true;
        }

        // "A > B > C" matches C directly, requiring its parent to match B,
        // whose parent must match A — fold right-to-left so the rightmost
        // compound ends up as the outermost (innermost-matching) selector.
        var result = compounds[^1];
        for (var i = compounds.Count - 2; i >= 0; i--)
        {
            result = new SelectorChild(compounds[i], result);
        }

        selector = result;
        return true;
    }

    /// <summary>
    /// Splits a selector's prelude into compound-selector segments separated by
    /// the child combinator '>'. Whitespace between two compounds WITHOUT an
    /// explicit '>' would be the descendant combinator, which RT's Selector API
    /// doesn't support (SelectorDescendant is internal and disabled) — this is
    /// rejected explicitly rather than silently misparsed.
    /// </summary>
    private static bool TrySplitCombinatorSegments(
        List<ICssComponentValue> prelude,
        out List<List<ICssComponentValue>> segments)
    {
        segments = new List<List<ICssComponentValue>>();
        var current = new List<ICssComponentValue>();
        var sawWhitespace = false;

        foreach (var item in prelude)
        {
            switch (item)
            {
                case WhitespaceToken:
                    sawWhitespace = true;
                    continue;
                case DelimToken { CodePoint: '>' }:
                    if (current.Count == 0)
                        return false; // '>' with nothing preceding it

                    segments.Add(current);
                    current = [];
                    sawWhitespace = false;
                    continue;
                default:
                    if (sawWhitespace && current.Count > 0)
                        return false; // whitespace between compounds with no explicit '>' — descendant combinator, unsupported

                    current.Add(item);
                    sawWhitespace = false;
                    break;
            }
        }

        if (current.Count == 0)
            return false; // trailing '>', or an empty prelude

        segments.Add(current);
        return true;
    }

    private static bool TryParseCompoundSelector(
        List<ICssComponentValue> segment,
        IReflectionManager reflection,
        [NotNullWhen(true)] out Selector? selector)
    {
        selector = null;
        var stream = new CssValueStream(segment);

        Type? type = null;
        string? id = null;
        var classes = new List<string>();
        var pseudoClasses = new List<string>();
        var extraSelectors = new List<Selector>();

        while (stream.Peek() is not null)
        {
            switch (stream.ConsumeNext())
            {
                case IdentToken ident:
                    if (!reflection.TryLooseGetType(RfCaseConversion.KebabToPascal(ident.Value), out type))
                        return Exception($"unknown control type '{ident.Value}'");

                    break;
                case DelimToken { CodePoint: '.' }:
                    if (stream.ConsumeNext() is IdentToken cls)
                        classes.Add(cls.Value);
                    break;
                case HashToken { IsId: true } hash:
                    id = hash.Value;
                    break;
                case ColonToken:
                    switch (stream.ConsumeNext())
                    {
                        case IdentToken { Value: "focus" }:
                            extraSelectors.Add(new FocusSelector());
                            break;
                        case IdentToken { Value: "root" }:
                            extraSelectors.Add(new RootSelector());
                            break;
                        case IdentToken { Value: "empty" }:
                            extraSelectors.Add(new EmptySelector());
                            break;
                        case IdentToken { Value: "first-child" }:
                            extraSelectors.Add(new NthChildSelector(new CssAnb(0, 1)));
                            break;
                        case IdentToken { Value: "last-child" }:
                            extraSelectors.Add(new NthLastChildSelector(new CssAnb(0, 1)));
                            break;
                        case IdentToken { Value: "first-of-type" }:
                            extraSelectors.Add(new NthOfTypeSelector(new CssAnb(0, 1)));
                            break;
                        case IdentToken { Value: "last-of-type" }:
                            extraSelectors.Add(new NthLastOfTypeSelector(new CssAnb(0, 1)));
                            break;
                        case IdentToken { Value: "only-child" }:
                            extraSelectors.Add(new OnlyChildSelector());
                            break;
                        case IdentToken { Value: "only-of-type" }:
                            extraSelectors.Add(new OnlyOfTypeSelector());
                            break;
                        case IdentToken pseudo:
                            pseudoClasses.Add(pseudo.Value);
                            break;
                        case CssFunction { Name: "nth-child" } nthChild:
                            if (!TryParseAnBMicrosyntax(nthChild.Value, out var anb))
                                return Exception("invalid An+B in nth-child()");

                            extraSelectors.Add(new NthChildSelector(anb.Value));
                            break;
                        case CssFunction { Name: "nth-of-type" } nthType:
                            if (!TryParseAnBMicrosyntax(nthType.Value, out anb))
                                return Exception("invalid An+B in nth-of-type()");

                            extraSelectors.Add(new NthOfTypeSelector(anb.Value));
                            break;
                        case CssFunction { Name: "nth-last-child" } nthChild:
                            if (!TryParseAnBMicrosyntax(nthChild.Value, out anb))
                                return Exception("invalid An+B in nth-last-child()");

                            extraSelectors.Add(new NthLastChildSelector(anb.Value));
                            break;
                        case CssFunction { Name: "nth-last-of-type" } nthType:
                            if (!TryParseAnBMicrosyntax(nthType.Value, out anb))
                                return Exception("invalid An+B in nth-last-of-type()");

                            extraSelectors.Add(new NthLastOfTypeSelector(anb.Value));
                            break;
                        case CssFunction { Name: "not" } not:
                            if (!TryGetSelectors(not.Value, reflection, out var inner))
                                return Exception("invalid content of not() selector");

                            extraSelectors.Add(new NotSelector(inner));
                            break;
                        case CssFunction { Name: "is" } @is:
                            if (!TryGetSelectors(@is.Value, reflection, out inner))
                                return Exception("invalid content of is() selector");

                            extraSelectors.Add(new OrSelector(inner));
                            break;
                        default:
                            return Exception("expected a pseudo-class or pseudo-function after ':'");
                    }
                    break;
            }
        }

        if (type == null && classes.Count == 0 && pseudoClasses.Count == 0 && id == null)
        {
            if (extraSelectors.Count == 1)
            {
                selector = extraSelectors[0];
                return true;
            }

            selector = new AndSelector(extraSelectors);
            return true;
        }

        var element = new SelectorElement(
            type != typeof(Control) ? type : null,
            classes.Count > 0 ? classes : null,
            id,
            pseudoClasses.Count > 0 ? pseudoClasses : null);

        selector = extraSelectors.Count == 0 ? element : new AndSelector([element, .. extraSelectors]);
        return true;

        bool Exception(string msg)
        {
#if EXCEPTION_TOLERANCE
            return false;
#else
            throw new CssParserError("resolving selector", msg);
#endif
        }
    }

    private static List<List<ICssComponentValue>> SplitByComma(List<ICssComponentValue> prelude)
    {
        var result = new List<List<ICssComponentValue>>();
        var current = new List<ICssComponentValue>();

        foreach (var item in prelude)
        {
            if (item is CommaToken)
            {
                result.Add(current);
                current = [];
                continue;
            }

            current.Add(item);
        }

        result.Add(current);
        return result;
    }
}
