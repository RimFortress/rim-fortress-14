using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Content.Client._RF.Suil;
// ReSharper disable ReturnValueOfPureMethodIsNotUsed

namespace Content.IntegrationTests.Tests._RF.Suil;

[TestFixture, TestOf(typeof(SuilTokenizer))]
public sealed class SuilTokenizerTests
{
    private static List<ISuilToken> Tokenize(string input)
        => SuilTokenizer.Tokenize(input).Select(t => t.Value).ToList();

    private static StringToken Str(params StringPart[] parts) => new(parts.ToImmutableArray());

    private static LocToken Loc(bool hasArguments, params StringPart[] parts) => new(parts.ToImmutableArray(), hasArguments);

    [Test]
    public void Whitespace_And_Comments_Collapse_Into_Single_Token()
    {
        var tokens = Tokenize("a  // line\n /* block */\t b");

        Assert.That(tokens, Is.EqualTo(new List<ISuilToken>
        {
            new IdentToken("a"), new WhitespaceToken(), new IdentToken("b"),
        }));
    }

    [Test]
    public void Comment_Between_Tokens_Separates_Them()
    {
        Assert.That(Tokenize("a/**/b"), Is.EqualTo(new List<ISuilToken>
        {
            new IdentToken("a"), new WhitespaceToken(), new IdentToken("b"),
        }));
    }

    [Test]
    public void Unterminated_Comment_Throws()
    {
        Assert.Throws<SuilTokenizeError>(() => Tokenize("a /* never closed"));
    }

    [Test]
    public void Line_Comment_May_End_At_Eof()
    {
        Assert.That(Tokenize("a // tail"), Is.EqualTo(new List<ISuilToken>
        {
            new IdentToken("a"), new WhitespaceToken(),
        }));
    }

    [Test]
    [TestCase("button", "button")]
    [TestCase("icon-sort-up", "icon-sort-up")]
    [TestCase("h1", "h1")]
    [TestCase("under_score", "under_score")]
    [TestCase("кнопка", "кнопка")]
    public void Ident_Token_Value(string input, string expected)
    {
        Assert.That(Tokenize(input), Is.EqualTo(new List<ISuilToken> { new IdentToken(expected) }));
    }

    [Test]
    public void Keywords_Are_Plain_Idents()
    {
        Assert.That(Tokenize("let use"), Is.EqualTo(new List<ISuilToken>
        {
            new IdentToken("let"), new WhitespaceToken(), new IdentToken("use"),
        }));
    }

    [Test]
    public void Single_Underscore_Is_Its_Own_Token()
    {
        Assert.That(Tokenize("+_ .flex-fill"), Is.EqualTo(new List<ISuilToken>
        {
            new DelimToken('+'), new UnderscoreToken(), new WhitespaceToken(),
            new DelimToken('.'), new IdentToken("flex-fill"),
        }));
    }

    [Test]
    [TestCase("_foo")]
    [TestCase("_1")]
    [TestCase("__")]
    public void Ident_Cannot_Start_With_Underscore(string input)
    {
        Assert.Throws<SuilTokenizeError>(() => Tokenize(input));
    }

    [Test]
    public void Function_Token_Needs_Glued_Parenthesis()
    {
        Assert.That(Tokenize("texture(\"a\") button (x)"), Is.EqualTo(new List<ISuilToken>
        {
            new FunctionToken("texture"), Str(StringPart.Literal("a")), new RightParenthesisToken(),
            new WhitespaceToken(), new IdentToken("button"), new WhitespaceToken(),
            new LeftParenthesisToken(), new IdentToken("x"), new RightParenthesisToken(),
        }));
    }

    [Test]
    [TestCase("#up", "up", true)]
    [TestCase("#delete-button", "delete-button", true)]
    [TestCase("#bad", "bad", true)]
    [TestCase("#B07D2B", "B07D2B", true)]
    [TestCase("#123456", "123456", false)]
    [TestCase("#5E4B3A59", "5E4B3A59", false)]
    public void Hash_Token_Reports_Whether_It_Can_Be_An_Id(string input, string value, bool isId)
    {
        Assert.That(Tokenize(input), Is.EqualTo(new List<ISuilToken> { new HashToken(value, isId) }));
    }

    [Test]
    public void Lone_Hash_Is_Delim()
    {
        Assert.That(Tokenize("# "), Is.EqualTo(new List<ISuilToken> { new DelimToken('#'), new WhitespaceToken() }));
    }

    [Test]
    public void Number_Tokens()
    {
        Assert.That(Tokenize("32 -4 .5 2.5 78% 16px 1fr 1e3"), Is.EqualTo(new List<ISuilToken>
        {
            new NumberToken(32, true), new WhitespaceToken(),
            new NumberToken(-4, true), new WhitespaceToken(),
            new NumberToken(0.5, false), new WhitespaceToken(),
            new NumberToken(2.5, false), new WhitespaceToken(),
            new PercentageToken(78), new WhitespaceToken(),
            new DimensionToken(16, true, "px"), new WhitespaceToken(),
            new DimensionToken(1, true, "fr"), new WhitespaceToken(),
            new NumberToken(1000, false),
        }));
    }

    [Test]
    public void Dot_Before_Ident_Is_A_Delim_Not_A_Number()
    {
        Assert.That(Tokenize(".icon-button"), Is.EqualTo(new List<ISuilToken>
        {
            new DelimToken('.'), new IdentToken("icon-button"),
        }));
    }

    [Test]
    [TestCase("\"hello\"", "hello")]
    [TestCase("\"\"", "")]
    [TestCase("\"a\\nb\"", "a\nb")]
    [TestCase("\"quote\\\"inside\"", "quote\"inside")]
    [TestCase("\"back\\\\slash\"", "back\\slash")]
    [TestCase("\"line\\\ncontinuation\"", "linecontinuation")]
    [TestCase("\"not \\`a var\\`\"", "not `a var`")]
    public void String_Token_Value_Is_Correctly_Unescaped(string input, string expected)
    {
        var parts = expected.Length == 0
            ? Array.Empty<StringPart>()
            : new[] { StringPart.Literal(expected) };

        Assert.That(Tokenize(input), Is.EqualTo(new List<ISuilToken> { Str(parts) }));
    }

    [Test]
    public void String_Interpolates_Variables()
    {
        Assert.That(Tokenize("\"a `p`-b `q`\""), Is.EqualTo(new List<ISuilToken>
        {
            Str(StringPart.Literal("a "), StringPart.Variable("p"), StringPart.Literal("-b "), StringPart.Variable("q")),
        }));
    }

    [Test]
    [TestCase("\"never closed")]
    [TestCase("\"new\nline\"")]
    [TestCase("\"`unclosed\"")]
    [TestCase("\"``\"")]
    [TestCase("\"`1a`\"")]
    public void Invalid_String_Throws(string input)
    {
        Assert.Throws<SuilTokenizeError>(() => Tokenize(input));
    }

    [Test]
    public void Variable_Outside_Of_String()
    {
        Assert.That(Tokenize("title: `title`,"), Is.EqualTo(new List<ISuilToken>
        {
            new IdentToken("title"), new ColonToken(), new WhitespaceToken(),
            new VariableToken("title"), new CommaToken(),
        }));
    }

    [Test]
    [TestCase("`")]
    [TestCase("`name")]
    [TestCase("``")]
    public void Invalid_Variable_Throws(string input)
    {
        Assert.Throws<SuilTokenizeError>(() => Tokenize(input));
    }

    [Test]
    public void Loc_Key_Is_Taken_Without_Quotes()
    {
        Assert.That(Tokenize("$ui-lobby-observe-button,"), Is.EqualTo(new List<ISuilToken>
        {
            Loc(false, StringPart.Literal("ui-lobby-observe-button")), new CommaToken(),
        }));
    }

    [Test]
    public void Loc_Key_Supports_Fluent_Attributes()
    {
        Assert.That(Tokenize("$message.attr"), Is.EqualTo(new List<ISuilToken>
        {
            Loc(false, StringPart.Literal("message.attr")),
        }));
    }

    [Test]
    public void Loc_Key_Interpolates_Variables()
    {
        Assert.That(Tokenize("$`p`-delete-button)"), Is.EqualTo(new List<ISuilToken>
        {
            Loc(false, StringPart.Variable("p"), StringPart.Literal("-delete-button")), new RightParenthesisToken(),
        }));
    }

    [Test]
    public void Loc_Key_With_Glued_Parenthesis_Has_Arguments()
    {
        Assert.That(Tokenize("$ui-count(n: `count`)"), Is.EqualTo(new List<ISuilToken>
        {
            Loc(true, StringPart.Literal("ui-count")),
            new IdentToken("n"), new ColonToken(), new WhitespaceToken(),
            new VariableToken("count"), new RightParenthesisToken(),
        }));
    }

    [Test]
    public void Loc_Key_With_Spaced_Parenthesis_Has_No_Arguments()
    {
        Assert.That(Tokenize("$key ("), Is.EqualTo(new List<ISuilToken>
        {
            Loc(false, StringPart.Literal("key")), new WhitespaceToken(), new LeftParenthesisToken(),
        }));
    }

    [Test]
    [TestCase("$")]
    [TestCase("$ key")]
    [TestCase("$.relative")]
    [TestCase("$,")]
    public void Invalid_Loc_Key_Throws(string input)
    {
        Assert.Throws<SuilTokenizeError>(() => Tokenize(input));
    }

    [Test]
    public void Content_Slot_Token()
    {
        Assert.That(Tokenize("col { %content% }"), Is.EqualTo(new List<ISuilToken>
        {
            new IdentToken("col"), new WhitespaceToken(), new LeftCurlyBracket(), new WhitespaceToken(),
            new ContentSlotToken("content"), new WhitespaceToken(), new RightCurlyBracket(),
        }));
    }

    [Test]
    [TestCase("%")]
    [TestCase("%content")]
    [TestCase("% x %")]
    [TestCase("%1%")]
    public void Percent_Sign_Without_A_Slot_Is_Delim(string input)
    {
        Assert.That(Tokenize(input)[0], Is.EqualTo(new DelimToken('%')));
    }

    [Test]
    public void Punctuation_And_Sigils()
    {
        Assert.That(Tokenize("+=>!|;:,{}()"), Is.EqualTo(new List<ISuilToken>
        {
            new DelimToken('+'), new DelimToken('='), new DelimToken('>'), new DelimToken('!'), new DelimToken('|'),
            new SemicolonToken(), new ColonToken(), new CommaToken(),
            new LeftCurlyBracket(), new RightCurlyBracket(), new LeftParenthesisToken(), new RightParenthesisToken(),
        }));
    }

    [Test]
    public void Element_Head_With_Access_Id_Classes_And_Props()
    {
        Assert.That(Tokenize("+button #up .icon-button (size: 36, disabled)"), Is.EqualTo(new List<ISuilToken>
        {
            new DelimToken('+'), new IdentToken("button"), new WhitespaceToken(),
            new HashToken("up", true), new WhitespaceToken(),
            new DelimToken('.'), new IdentToken("icon-button"), new WhitespaceToken(),
            new LeftParenthesisToken(), new IdentToken("size"), new ColonToken(), new WhitespaceToken(),
            new NumberToken(36, true), new CommaToken(), new WhitespaceToken(),
            new IdentToken("disabled"), new RightParenthesisToken(),
        }));
    }

    [Test]
    public void Hard_Property_Marker()
    {
        Assert.That(Tokenize("size: 32!"), Is.EqualTo(new List<ISuilToken>
        {
            new IdentToken("size"), new ColonToken(), new WhitespaceToken(),
            new NumberToken(32, true), new DelimToken('!'),
        }));
    }

    [Test]
    public void Tokens_Carry_Line_And_Column()
    {
        var tokens = SuilTokenizer.Tokenize("row {\n    // c\n    +_ #x\n}");

        Assert.That(tokens.Select(t => (t.Value, t.Line, t.Column)), Is.EqualTo(new (ISuilToken, int, int)[]
        {
            (new IdentToken("row"), 1, 1), (new WhitespaceToken(), 1, 4), (new LeftCurlyBracket(), 1, 5),
            (new WhitespaceToken(), 1, 6),
            (new DelimToken('+'), 3, 5), (new UnderscoreToken(), 3, 6), (new WhitespaceToken(), 3, 7),
            (new HashToken("x", true), 3, 8), (new WhitespaceToken(), 3, 10),
            (new RightCurlyBracket(), 4, 1),
        }));
    }

    [Test]
    public void Error_Reports_Line_And_Column()
    {
        var e = Assert.Throws<SuilTokenizeError>(() => SuilTokenizer.Tokenize("a\nb \"oops\n"));

        Assert.That(e!.Line, Is.EqualTo(2));
        Assert.That(e.Column, Is.GreaterThan(1));
    }

    [Test]
    public void Crlf_Is_Normalized()
    {
        var tokens = SuilTokenizer.Tokenize("a\r\nb");

        Assert.That(tokens[2].Line, Is.EqualTo(2));
        Assert.That(tokens[2].Column, Is.EqualTo(1));
    }

    [Test]
    public void Whole_File_Tokenizes()
    {
        const string source = """
            use "theme/lobby";

            let p = "character-setup-gui-character-picker-button";

            let tab-scroll = (title) {
                tab (title: `title`) { scroll .scroll-vertical { col { %content% } } }
            }

            .picker-row { gap: 4px; }

            button {
                row .picker-row {
                    +button #up .icon-button .open-both (size: 36, disabled) { _ .icon .icon-sort-up }
                    +confirm-button #delete-button (
                        text: $`p`-delete-button,
                        confirm-text: $`p`-confirm-delete-button
                    )
                }
            }
            """;

        var tokens = Tokenize(source);

        Assert.That(tokens.OfType<ContentSlotToken>().Count(), Is.EqualTo(1));
        Assert.That(tokens.OfType<LocToken>().Count(), Is.EqualTo(2));
        Assert.That(tokens.OfType<VariableToken>().Count(), Is.EqualTo(1));
    }
}
