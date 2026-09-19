using System.Collections.Generic;
using Content.Client._RF.CSS;
// ReSharper disable ReturnValueOfPureMethodIsNotUsed

namespace Content.IntegrationTests.Tests._RF.CSS;

[TestFixture, TestOf(typeof(CssTokenizer))]
public sealed class CssTokenizerTests
{
    private static List<ICssToken> Tokenize(string input) => CssTokenizer.Tokenize(input);

    [Test]
    public void Whitespace_Collapses_Into_Single_Token()
    {
        var tokens = Tokenize("a   \t\n  b");

        Assert.That(tokens, Has.Count.EqualTo(3));
        Assert.That(new IdentToken("a"), Is.EqualTo(tokens[0]));
        Assert.That(tokens[1], Is.TypeOf<WhitespaceToken>());
        Assert.That(new IdentToken("b"), Is.EqualTo(tokens[2]));
    }

    [Test]
    public void Comments_Are_Stripped_And_Do_Not_Produce_Tokens()
    {
        var tokens = Tokenize("a/* comment */b/**//**/c");

        Assert.That(
            new List<ICssToken> { new IdentToken("a"), new IdentToken("b"), new IdentToken("c") },
            Is.EqualTo(tokens));
    }

    [Test]
    public void Unterminated_Comment_Throws()
    {
        Assert.Throws<CssTokenizeError>(() => Tokenize("a /* never closed"));
    }

    [Test]
    [TestCase("\"hello\"", "hello")]
    [TestCase("'hello'", "hello")]
    [TestCase("\"line\\\ncontinuation\"", "linecontinuation")] // backslash-newline = line continuation, dropped
    [TestCase("\"esc\\61pe\"", "escape")]                      // \61 = hex escape for 'a'
    [TestCase("\"quote\\\"inside\"", "quote\"inside")]         // escaped quote doesn't end the string
    public void String_Token_Value_Is_Correctly_Unescaped(string input, string expectedValue)
    {
        var tokens = Tokenize(input);
        Assert.That(tokens, Has.Count.EqualTo(1));
        Assert.That(new StringToken(expectedValue), Is.EqualTo(tokens[0]));
    }

    [Test]
    public void Unterminated_String_Throws()
    {
        Assert.Throws<CssTokenizeError>(() => Tokenize("\"never closed"));
    }

    [Test]
    public void Newline_Inside_String_Throws()
    {
        Assert.Throws<CssTokenizeError>(() => Tokenize("\"broken\nstring\""));
    }

    [Test]
    [TestCase("123", 123, true)]
    [TestCase("-123", -123, true)]
    [TestCase("+123", 123, true)]
    [TestCase("1.5", 1.5, false)]
    [TestCase(".5", 0.5, false)]
    [TestCase("1e3", 1e3, false)]
    [TestCase("1E-3", 1E-3, false)]
    public void Number_Token_Parses_Value_And_Integer_Flag(string input, double expectedValue, bool expectedIsInteger)
    {
        var tokens = Tokenize(input);

        Assert.That(tokens, Has.Count.EqualTo(1));
        Assert.That(tokens[0], Is.TypeOf<NumberToken>());
        Assert.That(expectedValue, Is.EqualTo(((NumberToken)tokens[0]).Value));
        Assert.That(expectedIsInteger, Is.EqualTo(((NumberToken)tokens[0]).IsInteger));
    }

    [Test]
    public void Percentage_Token_Is_Produced_For_Trailing_Percent_Sign()
    {
        var tokens = Tokenize("50%");
        Assert.That(tokens, Has.Count.EqualTo(1));
        Assert.That(tokens[0], Is.EqualTo(new PercentageToken(50)));
    }

    [Test]
    [TestCase("10px", 10, "px")]
    [TestCase("1.5em", 1.5, "em")]
    [TestCase("100vh", 100, "vh")]
    public void Dimension_Token_Splits_Value_And_Unit(string input, double expectedValue, string expectedUnit)
    {
        var tokens = Tokenize(input);
        Assert.That(tokens, Has.Count.EqualTo(1));
        Assert.That(tokens[0], Is.TypeOf<DimensionToken>());

        var dimension = (DimensionToken)tokens[0];
        Assert.That(expectedValue, Is.EqualTo(dimension.Value));
        Assert.That(expectedUnit, Is.EqualTo(dimension.Unit));
    }

    [Test]
    public void Plus_Not_Followed_By_Number_Is_A_Delim_Token()
    {
        // regression test for the shifted-lookahead bug: "+-5" must NOT be read as a number
        var tokens = Tokenize("+-5");

        Assert.That(
            new List<ICssToken> { new DelimToken('+'), new NumberToken(-5, true) },
            Is.EqualTo(tokens));
    }

    [Test]
    public void Hash_Token_Sets_Id_Flag_Only_When_Value_Is_A_Valid_Ident()
    {
        var tokens = Tokenize("#foo");
        Assert.That(tokens, Has.Count.EqualTo(1));
        Assert.That(tokens[0], Is.TypeOf<HashToken>());

        var idHash = (HashToken)tokens[0];
        Assert.That(idHash.Value, Is.EqualTo("foo"));
        Assert.That(idHash.IsId, Is.True);

        tokens = Tokenize("#123");
        Assert.That(tokens, Has.Count.EqualTo(1));
        Assert.That(tokens[0], Is.TypeOf<HashToken>());

        var unrestrictedHash = (HashToken)tokens[0];
        Assert.That(unrestrictedHash.Value, Is.EqualTo("123"));
        Assert.That(unrestrictedHash.IsId, Is.False);
    }

    [Test]
    public void Double_Hyphen_Custom_Property_Name_Is_An_Ident_Token()
    {
        // regression test: "--foo" must tokenize as one ident, not delim + ident
        var tokens = Tokenize("--foo");

        Assert.That(tokens, Has.Count.EqualTo(1));
        Assert.That(tokens[0], Is.EqualTo(new IdentToken("--foo")));
    }

    [Test]
    public void Hyphen_Prefixed_Ident_Is_Recognized()
    {
        var tokens = Tokenize("-webkit-transform");

        Assert.That(tokens, Has.Count.EqualTo(1));
        Assert.That(tokens[0], Is.EqualTo(new IdentToken("-webkit-transform")));
    }

    [Test]
    public void Function_Token_Is_Produced_For_Ident_Followed_By_Open_Paren()
    {
        var tokens = Tokenize("rgb(");

        Assert.That(tokens, Has.Count.EqualTo(1));
        Assert.That(tokens[0], Is.EqualTo(new FunctionToken("rgb")));
    }

    [Test]
    [TestCase("url(foo.png)", "foo.png")]
    [TestCase("url( foo.png )", "foo.png")]
    [TestCase("URL(foo.png)", "foo.png")] // case-insensitive match on "url"
    public void Unquoted_Url_Produces_Url_Token(string input, string expectedValue)
    {
        var tokens = Tokenize(input);

        Assert.That(tokens, Has.Count.EqualTo(1));
        Assert.That(tokens[0], Is.EqualTo(new UrlToken(expectedValue)));
    }

    [Test]
    public void Quoted_Url_Is_Tokenized_As_Function_Plus_String_Not_As_A_Url_Token()
    {
        var tokens = Tokenize("url(\"foo.png\")");

        Assert.That(
            tokens,
            Is.EqualTo(new List<ICssToken>
            {
                new FunctionToken("url"),
                new StringToken("foo.png"),
                new RightParenthesisToken(),
            }));
    }

    [Test]
    public void Unquoted_Url_With_Space_In_The_Middle_Throws()
    {
        Assert.Throws<CssTokenizeError>(() => Tokenize("url(foo bar)"));
    }

    [Test]
    public void At_Keyword_Token_Is_Produced_For_At_Followed_By_Ident_Sequence()
    {
        var tokens = Tokenize("@media");

        Assert.That(tokens, Has.Count.EqualTo(1));
        Assert.That(tokens[0], Is.EqualTo(new AtKeywordToken("media")));
    }

    [Test]
    public void At_Not_Followed_By_Ident_Sequence_Is_A_Delim_Token()
    {
        var tokens = Tokenize("@!");

        Assert.That(
            tokens,
            Is.EqualTo(new List<ICssToken> { new DelimToken('@'), new DelimToken('!') }));
    }

    [Test]
    public void Cdo_And_Cdc_Tokens_Are_Recognized()
    {
        var tokens = Tokenize("<!-- -->");

        Assert.That(
            tokens,
            Is.EqualTo(new List<ICssToken> { new CdoToken(), new WhitespaceToken(), new CdcToken() }));
    }

    [Test]
    public void Structural_Single_Char_Tokens_Are_Recognized()
    {
        var tokens = Tokenize("{}()[]:;,");

        Assert.That(
            tokens,
            Is.EqualTo(new List<ICssToken>
            {
                new LeftCurlyBracket(), new RightCurlyBracket(),
                new LeftParenthesisToken(), new RightParenthesisToken(),
                new LeftSquareBracket(), new RightSquareBracket(),
                new ColonToken(), new SemicolonToken(), new CommaToken(),
            }));
    }

    [Test]
    public void Full_Declaration_Line_Tokenizes_As_Expected_Sequence()
    {
        var tokens = Tokenize("color: red;");

        Assert.That(
            tokens,
            Is.EqualTo(new List<ICssToken>
            {
                new IdentToken("color"),
                new ColonToken(),
                new WhitespaceToken(),
                new IdentToken("red"),
                new SemicolonToken(),
            }));
    }
}
