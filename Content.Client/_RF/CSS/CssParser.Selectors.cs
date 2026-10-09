using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Client._RF.CSS.Streams;
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
    /// <param name="reflection">Reflection manager. Used to get the <see cref="CssElementSelector.ElementType"/>.</param>
    /// <param name="resolver">CSS property access manager. Used for parsing <see cref="AttributeSelector"/>.</param>
    /// <param name="selectors">A selectors extracted from a rule.</param>
    /// <returns>True if the selector has been successfully parsed.</returns>
    /// <exception cref="CssParserError">
    /// If !EXCEPTION_TOLERANCE and the <paramref name="rule"/> contains syntax errors in the selector.
    /// </exception>
    [PublicAPI, Pure]
    public static bool TryGetSelectors(
        CssQualifiedRule rule,
        IReflectionManager reflection,
        ICssPropertyResolver resolver,
        [NotNullWhen(true)] out List<Selector>? selectors)
        => TryGetSelectors(rule.Prelude, reflection, resolver, out selectors);

    /// <summary>
    /// Extracts the element type a selector is scoped to, for property-registry
    /// lookup. Walks into <see cref="CompoundSelector"/>
    /// (compound with nth-child/nth-of-type etc.) and <see cref="SelectorChild"/> (child combinator,
    /// takes the rightmost/innermost compound — "A > B" applies to B) and
    /// <see cref="CssDescendantSelector"/> (the same for "A B").
    /// </summary>
    public static Type? GetElementType(Selector selector) => selector switch
    {
        CssElementSelector elem => elem.ElementType,
        CompoundSelector compound => compound.Selectors.OfType<CssElementSelector>().FirstOrDefault()?.ElementType, // TODO: maybe recursion?
        SelectorChild child => GetElementType(child.Child),
        CssDescendantSelector descendant => GetElementType(descendant.Descendant),
        _ => null,
    };

    private static bool TryGetSelectors(
        List<ICssComponentValue> prelude,
        IReflectionManager reflection,
        ICssPropertyResolver resolver,
        [NotNullWhen(true)] out List<Selector>? selectors)
    {
        selectors = new List<Selector>();

        foreach (var part in SplitByComma(prelude))
        {
            if (!TryGetSelectorFromPrelude(part, reflection, resolver, out var selector))
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
        ICssPropertyResolver resolver,
        [NotNullWhen(true)] out Selector? selector)
    {
        selector = null;

        if (!TrySplitCombinatorSegments(prelude, out var segments, out var descendantBefore))
        {
#if EXCEPTION_TOLERANCE
            return false;
#else
            throw new CssParserError("resolving selector", "expected compound selectors joined by '>' or whitespace");
#endif
        }

        var compounds = new List<Selector>(segments.Count);

        foreach (var segment in segments)
        {
            if (!TryParseCompoundSelector(segment, reflection, resolver, out var compound))
                return false; // inner call already threw when !EXCEPTION_TOLERANCE

            compounds.Add(compound);
        }

        if (compounds.Count == 1)
        {
            selector = compounds[0];
            return true;
        }

        // "A > B > C" matches C, whose parent must match "A > B" (B, and B's parent A): fold left-to-right,
        // so every parent chain is checked one level further up (the rightmost compound is the Child).
        // "A B" is the same with any ancestor instead of the parent, and the two mix: "A B > C" is C whose parent
        // is a B that has an ancestor A.
        var result = compounds[0];
        for (var i = 1; i < compounds.Count; i++)
        {
            result = descendantBefore[i]
                ? new CssDescendantSelector(result, compounds[i])
                : new SelectorChild(result, compounds[i]);
        }

        selector = result;
        return true;
    }

    /// <summary>
    /// Splits a selector's prelude into compound-selector segments separated by the child combinator '>' or by
    /// whitespace (the descendant combinator). Whitespace around '>' is not a combinator by itself.
    /// </summary>
    /// <param name="descendantBefore">
    /// For every segment, whether it is joined to the previous one by the descendant combinator (the first is never).
    /// </param>
    private static bool TrySplitCombinatorSegments(
        List<ICssComponentValue> prelude,
        out List<List<ICssComponentValue>> segments,
        out List<bool> descendantBefore)
    {
        segments = new List<List<ICssComponentValue>>();
        descendantBefore = new List<bool>();
        var current = new List<ICssComponentValue>();
        var sawWhitespace = false;
        var currentDescendant = false;

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
                    descendantBefore.Add(currentDescendant);
                    current = [];
                    currentDescendant = false;
                    sawWhitespace = false;
                    continue;
                default:
                    if (sawWhitespace && current.Count > 0)
                    {
                        // Whitespace between compounds with no explicit '>'.
                        segments.Add(current);
                        descendantBefore.Add(currentDescendant);
                        current = [];
                        currentDescendant = true;
                    }

                    current.Add(item);
                    sawWhitespace = false;
                    break;
            }
        }

        if (current.Count == 0)
            return false; // trailing '>', or an empty prelude

        segments.Add(current);
        descendantBefore.Add(currentDescendant);
        return true;
    }

    private static bool TryParseCompoundSelector(
        List<ICssComponentValue> segment,
        IReflectionManager reflection,
        ICssPropertyResolver resolver,
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
                    // TODO: Using `TryLooseGetType` here can be a huge problem, since it checks the
                    // suffix of the type name, which can cause conflicts with controls
                    // that have similar names—as I experienced with `LocaleTestCssControl` and `TestCssControl`.
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
                            if (!TryGetSelectors(not.Value, reflection, resolver, out var inner))
                                return Exception("invalid content of not() selector");

                            extraSelectors.Add(new NotSelector(inner));
                            break;
                        case CssFunction { Name: "is" } @is:
                            if (!TryGetSelectors(@is.Value, reflection, resolver, out inner))
                                return Exception("invalid content of is() selector");

                            extraSelectors.Add(new OrSelector(inner));
                            break;
                        default:
                            return Exception("expected a pseudo-class or pseudo-function after ':'");
                    }
                    break;
                case CssSimpleBlock { AssociatedToken: LeftSquareBracket } squareBlock:
                    string attrName;
                    var blockStream = new CssValueStream(squareBlock.Value);
                    var next = blockStream.ConsumeNext();

                    switch (next)
                    {
                        case StringToken str:
                            attrName = str.Value;
                            break;
                        case IdentToken ident:
                            attrName = ident.Value;
                            break;
                        default:
                            return Exception($"attribute selector property must be string or ident, not {next?.GetType().FullName}");
                    }

                    var resolveType = type ?? typeof(CssControl);

                    if (!resolver.TryGetPropertyAccessor(resolveType, attrName, out var accessor))
                        return Exception($"unknown attribute `{attrName}` for {resolveType.FullName}");

                    var op = blockStream.ConsumeNext() switch
                    {
                        DelimToken { CodePoint: '=' } => AttributeMatchOperator.Equals,
                        DelimToken { CodePoint: '~' } when blockStream.ConsumeNext() is DelimToken { CodePoint: '=' } => AttributeMatchOperator.WhitespaceList,
                        DelimToken { CodePoint: '|' } when blockStream.ConsumeNext() is DelimToken { CodePoint: '=' } => AttributeMatchOperator.HyphenList,
                        DelimToken { CodePoint: '^' } when blockStream.ConsumeNext() is DelimToken { CodePoint: '=' } => AttributeMatchOperator.StartsWith,
                        DelimToken { CodePoint: '$' } when blockStream.ConsumeNext() is DelimToken { CodePoint: '=' } => AttributeMatchOperator.EndsWith,
                        DelimToken { CodePoint: '*' } when blockStream.ConsumeNext() is DelimToken { CodePoint: '=' } => AttributeMatchOperator.Contains,
                        _ => (AttributeMatchOperator?)null,
                    };

                    if (op == null)
                        return Exception("unknown attribute selector operator");

                    // substring/prefix/suffix/list operators are inherently string
                    // operations — reject them for non-string properties
                    if (op != AttributeMatchOperator.Equals && accessor.Value.ClrType != typeof(string))
                        return Exception($"operator only valid for string-typed properties, '{attrName}' is {accessor.Value.ClrType.Name}");

                    if (!resolver.TryParseValue(ref blockStream, accessor.Value.ClrType, out var comparisonValue))
                        return Exception($"failed to parse attribute selector value of field '{attrName}'");

                    var comparison = StringComparison.Ordinal;

                    if (blockStream.Peek() is IdentToken { Value: "i" or "I" })
                    {
                        blockStream.ConsumeNext();
                        comparison = StringComparison.OrdinalIgnoreCase;
                    }
                    else if (blockStream.Peek() is IdentToken { Value: "s" or "S" })
                    {
                        blockStream.ConsumeNext();
                        comparison = StringComparison.Ordinal; // explicit default, just consumes the token
                    }

                    if (blockStream.ConsumeNext() != null)
                        return Exception("unexpected content after attribute selector value, expected ']'");

                    extraSelectors.Add(new AttributeSelector(accessor.Value.Getter, op.Value, comparisonValue, comparison));
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

        var element = new CssElementSelector(
            type != typeof(Control) && type != typeof(CssControl) ? type : null,
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
