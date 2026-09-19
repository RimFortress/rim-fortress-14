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
    /// <param name="selector">A selector extracted from a rule.</param>
    /// <returns>True if the selector has been successfully parsed.</returns>
    /// <exception cref="CssParserError">
    /// If !EXCEPTION_TOLERANCE and the <see cref="SelectorElement.ElementType"/> could not be found.
    /// </exception>
    [PublicAPI, Pure]
    public static bool TryGetSelector(
        CssQualifiedRule rule,
        IReflectionManager reflection,
        [NotNullWhen(true)] out Selector? selector)
    {
        selector = null;

        if (!TrySplitCombinatorSegments(rule.Prelude, out var segments))
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
                    if (!reflection.TryLooseGetType(RfCaseConversion.CapitalizeFirst(ident.Value), out type))
                    {
    #if EXCEPTION_TOLERANCE
                        selector = null;
                        return false;
    #else
                        throw new CssParserError("resolving selector", $"unknown control type '{ident.Value}'");
    #endif
                    }
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
                        case IdentToken pseudo:
                            pseudoClasses.Add(pseudo.Value);
                            break;
                        case CssFunction { Name: "nth-child" } nthChild:
                            if (!TryParseAnBMicrosyntax(nthChild.Value, out var anbChild))
                            {
    #if EXCEPTION_TOLERANCE
                                selector = null;
                                return false;
    #else
                                throw new CssParserError("resolving selector", $"invalid An+B in {nthChild.Name}()");
    #endif
                            }
                            extraSelectors.Add(new NthChildSelector(anbChild.Value));
                            break;
                        case CssFunction { Name: "nth-of-type" } nthType:
                            if (!TryParseAnBMicrosyntax(nthType.Value, out var anbType))
                            {
    #if EXCEPTION_TOLERANCE
                                selector = null;
                                return false;
    #else
                                throw new CssParserError("resolving selector", $"invalid An+B in {nthType.Name}()");
    #endif
                            }
                            extraSelectors.Add(new NthOfTypeSelector(anbType.Value));
                            break;
                        default:
    #if EXCEPTION_TOLERANCE
                            selector = null;
                            return false;
    #else
                            throw new CssParserError("resolving selector", "expected a pseudo-class or pseudo-function after ':'");
    #endif
                    }
                    break;
            }
        }

        var element = new SelectorElement(
            type != typeof(Control) ? type : null,
            classes.Count > 0 ? classes : null,
            id,
            pseudoClasses.Count > 0 ? pseudoClasses : null);

        selector = extraSelectors.Count == 0 ? element : new AndSelector([element, .. extraSelectors]);
        return true;
    }
}
