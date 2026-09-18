using System.Diagnostics.CodeAnalysis;
using Content.Shared._RF.Utilities;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Reflection;

namespace Content.Client._RF.CSS;

/// <summary>
/// A CCS token parser written according to the specification at https://www.w3.org/TR/css-syntax-3/
/// </summary>
public static class CssParser
{
    /// <summary>
    /// Parses CSS text into a set of style rules.
    /// This is the function you want to use to parse CSS files.
    /// </summary>
    [PublicAPI, Pure]
    public static List<IParsedCssObject> ParseStylesheet(string text)
        => ParseStylesheet(CssTokenizer.Tokenize(text));

    /// <summary>
    /// Parses CSS tokens into a set of style rules.
    /// This is the function you want to use to parse CSS files.
    /// </summary>
    [PublicAPI, Pure]
    public static List<IParsedCssObject> ParseStylesheet(List<ICssToken> tokens)
    {
        var stream = new CssTokenStream(tokens);
        return ConsumeListOfRules(ref stream, topLevelFlag: true);
    }

    /// <summary>
    /// Parses CSS tokens into a set of style rules.
    /// This is the function you want to use to parse CSS files.
    /// </summary>
    [PublicAPI, Pure]
    public static List<IParsedCssObject> ParseStylesheet(ICssComponentValue[] tokens)
    {
        var stream = new CssTokenStream(tokens);
        return ConsumeListOfRules(ref stream, topLevelFlag: true);
    }

    /// <summary>
    /// It parses the contents of a single block of CSS rules.
    /// This is the function you'll want to use to parse the
    /// contents of the `style` attribute or something similar.
    /// </summary>
    [PublicAPI, Pure]
    public static List<IParsedCssObject> ParseListOfDeclarations(string text)
    {
        var tokens = CssTokenizer.Tokenize(text);
        var stream = new CssTokenStream(tokens);
        return ConsumeListOfDeclarations(ref stream);
    }

    /// <summary>
    /// It parses the tokens of a single block of CSS rules.
    /// </summary>
    [PublicAPI, Pure]
    public static List<IParsedCssObject> ParseListOfDeclarations(ICssComponentValue[] tokens)
    {
        var stream = new CssTokenStream(tokens);
        return ConsumeListOfDeclarations(ref stream);
    }

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
        var stream = new CssValueStream(rule.Prelude);

        Type? type = null;
        string? id = null;
        var classes = new List<string>();
        var pseudoClasses = new List<string>();

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
                    if (stream.ConsumeNext() is IdentToken pseudo)
                        pseudoClasses.Add(pseudo.Value);
                    break;
            }
        }

        selector = new SelectorElement(
            type != typeof(Control) ? type : null,
            classes.Count > 0 ? classes : null,
            id,
            pseudoClasses.Count > 0 ? pseudoClasses : null);
        return true;
    }

    /// <summary>
    /// https://www.w3.org/TR/css-syntax-3/#consume-list-of-rules
    /// </summary>
    private static List<IParsedCssObject> ConsumeListOfRules(ref CssTokenStream stream, bool topLevelFlag = false)
    {
        var result = new List<IParsedCssObject>();

        while (true)
        {
            var token = stream.ConsumeNext();

            switch (token)
            {
                case WhitespaceToken:
                    continue;
                case CdoToken or CdcToken:
                    if (topLevelFlag)
                        continue;

                    stream.Reconsume();
                    result.Add(ConsumeQualifiedRule(ref stream));
                    continue;
                case EofToken:
                    return result;
                case AtKeywordToken:
                    stream.Reconsume();
                    result.Add(ConsumeAtRule(ref stream));
                    continue;
                default:
                    stream.Reconsume();
                    result.Add(ConsumeQualifiedRule(ref stream));
                    continue;
            }
        }
    }

    /// <summary>
    /// https://www.w3.org/TR/css-syntax-3/#consume-at-rule
    /// </summary>
    private static CssAtRule ConsumeAtRule(ref CssTokenStream stream)
    {
        var nameToken = (AtKeywordToken)stream.ConsumeNext();
        var rule = new CssAtRule(nameToken.Value);

        while (true)
        {
            var token = stream.ConsumeNext();

            switch (token)
            {
                case SemicolonToken:
                    return rule;
                case EofToken:
                    throw new CssParserError("consuming at-rule", "unexpected EOF");
                case LeftCurlyBracket:
                    rule.Block = ConsumeSimpleBlock(ref stream);
                    return rule;
                default:
                    stream.Reconsume();
                    rule.Prelude.Add(ConsumeComponentValue(ref stream));
                    break;
            }
        }
    }

    /// <summary>
    /// https://www.w3.org/TR/css-syntax-3/#consume-qualified-rule
    /// </summary>
    private static CssQualifiedRule ConsumeQualifiedRule(ref CssTokenStream stream)
    {
        var rule = new CssQualifiedRule();

        while (true)
        {
            var token = stream.ConsumeNext();

            switch (token)
            {
                case EofToken:
                    throw new CssParserError("consuming qualified rule", "unexpected EOF");
                case LeftCurlyBracket:
                    rule.Block = ConsumeSimpleBlock(ref stream);
                    return rule;
                case CssSimpleBlock block:
                    rule.Block = block;
                    return rule;
                default:
                    stream.Reconsume();
                    rule.Prelude.Add(ConsumeComponentValue(ref stream));
                    continue;
            }
        }
    }

    /// <summary>
    /// https://www.w3.org/TR/css-syntax-3/#consume-list-of-declarations
    /// </summary>
    private static List<IParsedCssObject> ConsumeListOfDeclarations(ref CssTokenStream stream)
    {
        var result = new List<IParsedCssObject>();

        while (true)
        {
            var token = stream.ConsumeNext();

            switch (token)
            {
                case WhitespaceToken or SemicolonToken:
                    continue;
                case EofToken:
                    return result;
                case AtKeywordToken:
                    stream.Reconsume();
                    result.Add(ConsumeAtRule(ref stream));
                    break;
                case IdentToken:
                    stream.Reconsume();
                    var s = new CssTokenStream(CollectUntilSemicolonOrEof(ref stream));
                    result.Add(ConsumeDeclaration(ref s));
                    break;
                default:
                    throw new CssParserError(
                        "consuming list of declarations",
                        $"unexpected {token.GetType().Name}, expected a declaration or at-rule");
            }
        }
    }

    /// <summary>
    /// https://www.w3.org/TR/css-syntax-3/#consume-declaration
    /// </summary>
    private static CssDeclaration ConsumeDeclaration(ref CssTokenStream stream)
    {
        var token = (IdentToken)stream.ConsumeNext();
        var declaration = new CssDeclaration(token.Value);

        while (stream.PeekNext() is WhitespaceToken)
        {
            stream.ConsumeNext();
        }

        if (stream.PeekNext() is not ColonToken)
        {
            throw new CssParserError("consuming declaration",
                $"expected ColonToken, not {stream.PeekNext().GetType().Name}");
        }

        stream.ConsumeNext();

        while (stream.PeekNext() is WhitespaceToken)
        {
            stream.ConsumeNext();
        }

        while (stream.PeekNext() is not EofToken)
        {
            declaration.Value.Add(ConsumeComponentValue(ref stream));
        }

        StripImportantFlag(declaration);
        TrimTrailingWhitespace(declaration);

        return declaration;
    }

    /// <summary>
    /// Consume a component value. This is the workhorse every other algorithm
    /// below calls into — it's what turns "{"/"("/"[" and function-tokens into
    /// their folded representations, and passes everything else through as-is.
    /// https://www.w3.org/TR/css-syntax-3/#consume-component-value
    /// </summary>
    private static ICssComponentValue ConsumeComponentValue(ref CssTokenStream stream)
    {
        var token = stream.ConsumeNext();

        return token switch
        {
            LeftCurlyBracket or LeftParenthesisToken or LeftSquareBracket => ConsumeSimpleBlock(ref stream),
            FunctionToken => ConsumeFunction(ref stream),
            _ => token,
        };
    }

    /// <summary>
    /// https://www.w3.org/TR/css-syntax-3/#consume-simple-block
    /// </summary>
    private static CssSimpleBlock ConsumeSimpleBlock(ref CssTokenStream stream)
    {
        var ending = stream.Current switch
        {
            LeftCurlyBracket => typeof(RightCurlyBracket),
            LeftParenthesisToken => typeof(RightParenthesisToken),
            LeftSquareBracket => typeof(RightSquareBracket),
            _ => throw new CssParserError("consuming simple block",
                $"unexpected simple block opening token: {stream.Current.GetType().Name}"),
        };

        var block = new CssSimpleBlock((ICssToken)stream.Current, new());

        while (true)
        {
            var token = stream.ConsumeNext();

            if (token.GetType() == ending)
                return block;

            if (token is EofToken)
            {
                throw new CssParserError("consuming simple block",
                    $"expected simple block closing token {ending.Name}, not EOF");
            }

            stream.Reconsume();
            block.Value.Add(ConsumeComponentValue(ref stream));
        }
    }

    /// <summary>
    /// https://www.w3.org/TR/css-syntax-3/#consume-function
    /// </summary>
    private static CssFunction ConsumeFunction(ref CssTokenStream stream)
    {
        var func = new CssFunction(((FunctionToken)stream.Current).Value, new());

        while (true)
        {
            var token = stream.ConsumeNext();

            switch (token)
            {
                case RightParenthesisToken:
                    return func;
                case EofToken:
                    throw new CssParserError("consuming function", "expected RightParenthesisToken, not EOF");
                default:
                    stream.Reconsume();
                    func.Value.Add(ConsumeComponentValue(ref stream));
                    break;
            }
        }
    }

    private static void StripImportantFlag(CssDeclaration declaration)
    {
        var i = declaration.Value.Count - 1;
        while (i >= 0 && declaration.Value[i] is WhitespaceToken)
        {
            i--;
        }

        var lastIdx = i;
        i--;

        while (i >= 0 && declaration.Value[i] is WhitespaceToken)
        {
            i--;
        }
        var secondLastIdx = i;

        if (lastIdx < 0 || secondLastIdx < 0)
            return;

        if (declaration.Value[secondLastIdx] is not DelimToken { CodePoint: 0x0021 }) // !
            return;

        if (declaration.Value[lastIdx] is not IdentToken { Value: var value }
            || !value.Equals("important", StringComparison.OrdinalIgnoreCase))
            return;

        declaration.Value.RemoveAt(lastIdx); // remove higher index first
        declaration.Value.RemoveAt(secondLastIdx);
        declaration.Important = true;
    }

    private static void TrimTrailingWhitespace(CssDeclaration declaration)
    {
        while (declaration.Value.Count > 0 && declaration.Value[^1] is WhitespaceToken)
        {
            declaration.Value.RemoveAt(declaration.Value.Count - 1);
        }
    }

    /// <summary>
    /// "Initialize a temp list filled with the current input token, then consume
    /// component values into it until ; or EOF" (§5.4.5/5.4.6). Note this collects
    /// component values, not raw tokens — a function/block met along the way is
    /// already folded by ConsumeComponentValue by the time it lands in this list.
    /// </summary>
    private static ICssComponentValue[] CollectUntilSemicolonOrEof(ref CssTokenStream stream)
    {
        var items = new List<ICssComponentValue> { stream.ConsumeNext() };

        while (stream.PeekNext() is not (SemicolonToken or EofToken))
        {
            items.Add(ConsumeComponentValue(ref stream));
        }

        return items.ToArray();
    }
}
