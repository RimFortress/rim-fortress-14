namespace Content.Client._RF.Css;

public static class CssParser
{
    public static List<object> ParseStylesheet(List<ICssToken> tokens)
    {
        var stream = new CssTokenStream(tokens);
        return ConsumeListOfRules(ref stream, topLevelFlag: true);
    }

    public static List<object> ParseListOfDeclarations(ICssComponentValue[] items)
    {
        var stream = new CssTokenStream(items);
        return ConsumeListOfDeclarations(ref stream);
    }

    private static List<object> ConsumeListOfRules(ref CssTokenStream stream, bool topLevelFlag = false)
    {
        var result = new List<object>();

        while (true)
        {
            var token = stream.ConsumeNext();

            switch (token)
            {
                case WhitespaceToken or CdoToken or CdcToken:
                    continue;
                case EofToken:
                    return result;
                case AtKeywordToken:
                    stream.Reconsume();
                    result.Add(ConsumeAtRule(ref stream));
                    break;
            }
        }
    }

    private static List<object> ConsumeListOfDeclarations(ref CssTokenStream stream)
    {
        var result = new List<object>();

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
                    // TODO
            }
        }
    }

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

    private static CssSimpleBlock ConsumeSimpleBlock(ref CssTokenStream stream)
    {
        ICssToken ending = stream.Current switch
        {
            LeftCurlyBracket => new RightCurlyBracket(),
            LeftParenthesisToken => new RightParenthesisToken(),
            LeftSquareBracket => new RightSquareBracket(),
            _ => throw new CssParserError("consuming simple block",
                $"unexpected simple block opening token: {stream.Current.GetType().Name}"),
        };

        var block = new CssSimpleBlock((ICssToken)stream.Current, new());

        while (true)
        {
            var token = stream.ConsumeNext();

            if (token == ending)
                return block;

            if (token is EofToken)
            {
                throw new CssParserError("consuming simple block",
                    $"expected simple block closing token {ending.GetType().Name}, not EOF");
            }

            stream.Reconsume();
            block.Value.Add(ConsumeComponentValue(ref stream));
        }
    }

    /// <summary>
    /// Consume a component value. This is the workhorse every other algorithm
    /// below calls into — it's what turns "{"/"("/"[" and function-tokens into
    /// their folded representations, and passes everything else through as-is.
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
}
