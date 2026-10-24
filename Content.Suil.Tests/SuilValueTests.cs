using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Content.Suil;

namespace Content.Suil.Tests;

[TestFixture]
public sealed class SuilValueTests
{
    private static List<ISuilValue> Declaration(string value)
    {
        var rule = (SuilRule) SuilParser.ParseFile($".a {{ v: {value}; }}").Items.Single();
        return rule.Declarations.Single().Value;
    }

    // ---- Syntax of the values ----

    [Test]
    public void Commas_Are_Values_In_Declarations()
    {
        Assert.That(Declaration("\"A B\", Arial, sans-serif"), Is.EqualTo(new List<ISuilValue>
        {
            new SuilString(ImmutableArray.Create(StringPart.Literal("A B"))), new SuilComma(), new SuilIdent("Arial"),
            new SuilComma(), new SuilIdent("sans-serif"),
        }));
    }

    [Test]
    public void Commas_Are_Values_In_Let()
    {
        var let = (SuilLet) SuilParser.ParseFile("let stack = a, b;").Items.Single();

        Assert.That(((SuilValueList) let.Value).Values, Is.EqualTo(new List<ISuilValue>
        {
            new SuilIdent("a"), new SuilComma(), new SuilIdent("b"),
        }));
    }

    [Test]
    public void Commas_Still_End_The_Properties_Of_An_Element()
    {
        var root = SuilParser.ParseFile("row (a: 1, b: 2)").Root!;

        Assert.That(root.Properties.Select(p => p.Name), Is.EqualTo(new[] { "a", "b" }));
    }

    [Test]
    public void Slash_And_Operators_Are_Values()
    {
        Assert.That(Declaration("12px/1.5 a / b"), Is.EqualTo(new List<ISuilValue>
        {
            new SuilDimension(12, true, "px"), new SuilDelim('/'), new SuilNumber(1.5, false),
            new SuilIdent("a"), new SuilDelim('/'), new SuilIdent("b"),
        }));
    }

    [Test]
    public void Calc_Is_A_Function_With_Operators()
    {
        var value = (SuilFunction) Declaration("calc(100% - 10px * 2)").Single();

        Assert.That(value.Name, Is.EqualTo("calc"));
        Assert.That(value.Arguments.Single(), Is.EqualTo(new List<ISuilValue>
        {
            new SuilPercentage(100), new SuilDelim('-'), new SuilDimension(10, true, "px"), new SuilDelim('*'),
            new SuilNumber(2, true),
        }));
    }

    [Test]
    public void Parenthesized_Group_Is_A_Function_Without_A_Name()
    {
        var calc = (SuilFunction) Declaration("calc((1px + 2px) * 3)").Single();
        var group = (SuilFunction) calc.Arguments.Single()[0];

        Assert.That(group.Name, Is.Empty);
        Assert.That(group.Arguments.Single(), Has.Count.EqualTo(3));
    }

    [Test]
    public void Names_May_Start_With_A_Hyphen()
    {
        var rule = (SuilRule) SuilParser.ParseFile(".a { -webkit-text-stroke: 2px; margin: 1px -2px; v: -webkit-box; }").Items.Single();

        Assert.That(rule.Declarations.Select(d => d.Name), Is.EqualTo(new[] { "-webkit-text-stroke", "margin", "v" }));
        Assert.That(rule.Declarations[1].Value, Is.EqualTo(new List<ISuilValue>
        {
            new SuilDimension(1, true, "px"), new SuilDimension(-2, true, "px"),
        }));
        Assert.That(rule.Declarations[2].Value.Single(), Is.EqualTo(new SuilIdent("-webkit-box")));
    }

    [Test]
    public void A_Lone_Minus_Is_An_Operator()
    {
        Assert.That(Declaration("calc(1px - 2px)"), Has.Count.EqualTo(1));
        Assert.That(((SuilFunction) Declaration("calc(1px - 2px)").Single()).Arguments.Single()[1], Is.EqualTo(new SuilDelim('-')));
    }

    [Test]
    public void Let_Values_Are_Spliced_With_Their_Commas()
    {
        var file = SuilExpander.Expand("let stack = a, b;\n.x { font-family: c, <stack>; }");
        var rule = (SuilRule) file.Items.Single();

        Assert.That(rule.Declarations.Single().Value, Is.EqualTo(new List<ISuilValue>
        {
            new SuilIdent("c"), new SuilComma(), new SuilIdent("a"), new SuilComma(), new SuilIdent("b"),
        }));
    }

    [Test]
    public void Important_Declarations()
    {
        var rule = (SuilRule) SuilParser.ParseFile(".a { x: 1 !important; y: 2 ! IMPORTANT; z: 3; w: 4 !important }").Items.Single();

        Assert.That(rule.Declarations.Select(d => (d.Name, d.Important)),
            Is.EqualTo(new[] { ("x", true), ("y", true), ("z", false), ("w", true) }));
        Assert.That(rule.Declarations[0].Value, Is.EqualTo(new List<ISuilValue> { new SuilNumber(1, true) }));
    }

    [Test]
    [TestCase(".a { x: 1 ! ; }")]
    [TestCase(".a { x: 1 !hard; }")]
    [TestCase(".a { x: !important; }")]
    public void Malformed_Important_Throws(string input)
    {
        Assert.Throws<SuilParserError>(() => SuilParser.ParseFile(input));
    }

    [Test]
    public void Custom_Properties_And_Var_Calls()
    {
        var rule = (SuilRule) SuilParser.ParseFile(
            ":root { --gold: #B07D2B; } .a { color: var(--gold); border: 1px solid var(--gold, red); }").Items[0];

        Assert.That(rule.Declarations.Single().Name, Is.EqualTo("--gold"));

        var second = (SuilRule) SuilParser.ParseFile(".a { border: 1px solid var(--gold, red, blue); }").Items.Single();
        var call = (SuilFunction) second.Declarations.Single().Value[2];

        Assert.That(call.Name, Is.EqualTo("var"));
        Assert.That(call.Arguments[0].Single(), Is.EqualTo(new SuilIdent("--gold")));
        Assert.That(call.Arguments, Has.Count.EqualTo(3));
    }

    // ---- The stream ----

    [Test]
    public void Stream_Reads_Marks_And_Resets()
    {
        var stream = new SuilValueStream(new List<ISuilValue> { new SuilIdent("a"), new SuilComma(), new SuilIdent("b") });

        Assert.That(stream.AtEnd, Is.False);
        Assert.That(stream.Peek(), Is.EqualTo(new SuilIdent("a")));

        var mark = stream.Mark();
        Assert.That(stream.ConsumeNext(), Is.EqualTo(new SuilIdent("a")));
        Assert.That(stream.TryConsumeComma(), Is.True);
        Assert.That(stream.TryConsumeComma(), Is.False);
        Assert.That(stream.ConsumeNext(), Is.EqualTo(new SuilIdent("b")));
        Assert.That(stream.AtEnd, Is.True);
        Assert.That(stream.ConsumeNext(), Is.Null);
        Assert.That(stream.Peek(), Is.Null);

        stream.Reset(mark);
        Assert.That(stream.Peek(), Is.EqualTo(new SuilIdent("a")));
    }

    [Test]
    public void Function_Arguments_Become_A_Stream_With_Commas()
    {
        var function = (SuilFunction) Declaration("f(1 2, 3)").Single();
        var stream = SuilValueStream.OfArguments(function);

        Assert.That(stream.Items, Is.EqualTo(new List<ISuilValue>
        {
            new SuilNumber(1, true), new SuilNumber(2, true), new SuilComma(), new SuilNumber(3, true),
        }));
    }

    // ---- The parsers ----

    private enum Mode
    {
        Plain,
        FlexStart,
        RowReverse,
    }

    private sealed class IntParser : SuilValueParser<int>
    {
        public override bool TryParse(ref SuilValueStream stream, out int value)
        {
            value = 0;

            if (stream.Peek() is not SuilNumber { IsInteger: true } number)
                return false;

            stream.ConsumeNext();
            value = (int) number.Value;
            return true;
        }
    }

    private sealed class PairParser : SuilValueParser<(int, int)>
    {
        public override bool TryParse(ref SuilValueStream stream, out (int, int) value)
        {
            value = default;

            if (!Values.TryParse<int>(ref stream, out var a) || !Values.TryParse<int>(ref stream, out var b))
                return false;

            value = (a, b);
            return true;
        }
    }

    private sealed class EvenParser : SuilValueParser<int>
    {
        public override bool Default => false;

        public override bool TryParse(ref SuilValueStream stream, out int value)
        {
            value = 0;

            if (stream.Peek() is not SuilNumber { IsInteger: true, Value: var v } || (int) v % 2 != 0)
                return false;

            stream.ConsumeNext();
            value = (int) v;
            return true;
        }
    }

    private static SuilValueParsers Make()
    {
        var parsers = new SuilValueParsers();
        parsers.Register(new IntParser());
        parsers.Register(new PairParser());
        parsers.Register(new EvenParser());
        return parsers;
    }

    private static SuilValueStream Stream(string value) => new(Declaration(value));

    [Test]
    public void Default_Parser_Consumes_Only_Its_Part()
    {
        var parsers = Make();
        var stream = Stream("1 2 3");

        Assert.That(parsers.TryParse<int>(ref stream, out var first), Is.True);
        Assert.That(first, Is.EqualTo(1));
        Assert.That(stream.Peek(), Is.EqualTo(new SuilNumber(2, true)));
    }

    [Test]
    public void Parsers_Can_Use_Other_Parsers()
    {
        var parsers = Make();
        var stream = Stream("4 5");

        Assert.That(parsers.TryParse<(int, int)>(ref stream, out var pair), Is.True);
        Assert.That(pair, Is.EqualTo((4, 5)));
        Assert.That(stream.AtEnd, Is.True);
    }

    [Test]
    public void A_Failed_Parser_Leaves_The_Stream_Where_It_Was()
    {
        var parsers = Make();
        var stream = Stream("1 x");

        Assert.That(parsers.TryParse<(int, int)>(ref stream, out _), Is.False);
        Assert.That(stream.Peek(), Is.EqualTo(new SuilNumber(1, true)).Or.EqualTo(new SuilIdent("x")));
    }

    [Test]
    public void Custom_Parser_Is_Used_Only_By_Name()
    {
        var parsers = Make();

        var odd = Stream("3");
        Assert.That(parsers.TryParse<int>(ref odd, out var three), Is.True, "the default parser takes any integer");
        Assert.That(three, Is.EqualTo(3));

        odd = Stream("3");
        Assert.That(parsers.TryParseWith(typeof(EvenParser), ref odd, out _), Is.False);

        var even = Stream("4");
        Assert.That(parsers.TryParseWith(typeof(EvenParser), ref even, out var four), Is.True);
        Assert.That(four, Is.EqualTo(4));
    }

    [Test]
    public void An_Unregistered_Custom_Parser_Is_An_Error()
    {
        var stream = Stream("1");

        Assert.Throws<ArgumentException>(() => Make().TryParseWith(typeof(string), ref stream, out _));
    }

    [Test]
    public void A_Type_Without_A_Parser_Is_An_Error()
    {
        var stream = Stream("1");

        Assert.Throws<ArgumentException>(() => Make().TryParse<Guid>(ref stream, out _));
    }

    [Test]
    public void Enums_Are_Parsed_By_The_Kebab_Case_Name()
    {
        var parsers = Make();

        foreach (var (text, expected) in new[] { ("plain", Mode.Plain), ("flex-start", Mode.FlexStart), ("Row-Reverse", Mode.RowReverse) })
        {
            var stream = Stream(text);
            Assert.That(parsers.TryParse<Mode>(ref stream, out var mode), Is.True, text);
            Assert.That(mode, Is.EqualTo(expected));
        }

        var bad = Stream("unknown");
        Assert.That(parsers.TryParse<Mode>(ref bad, out _), Is.False);

        var number = Stream("1");
        Assert.That(parsers.TryParse<Mode>(ref number, out _), Is.False);
        Assert.That(parsers.HasDefault(typeof(Mode)), Is.True);
        Assert.That(parsers.HasDefault(typeof(Mode?)), Is.True);
        Assert.That(parsers.HasDefault(typeof(Guid)), Is.False);
    }

    [Test]
    public void None_Is_The_Null_Of_A_Nullable_Type()
    {
        var parsers = Make();

        var none = Stream("none");
        Assert.That(parsers.TryParse<int?>(ref none, out var nothing), Is.True);
        Assert.That(nothing, Is.Null);
        Assert.That(none.AtEnd, Is.True);

        var value = Stream("7");
        Assert.That(parsers.TryParse<int?>(ref value, out var seven), Is.True);
        Assert.That(seven, Is.EqualTo(7));

        var mode = Stream("none");
        Assert.That(parsers.TryParse<Mode?>(ref mode, out var noMode), Is.True);
        Assert.That(noMode, Is.Null);
    }

    private sealed class Texture
    {
    }

    private sealed class TextureParser : SuilValueParser<Texture>
    {
        public override bool TryParse(ref SuilValueStream stream, [NotNullWhen(true)] out Texture? value)
        {
            value = null;

            if (stream.Peek() is not SuilFunction { Name: "texture" })
                return false;

            stream.ConsumeNext();
            value = new Texture();
            return true;
        }
    }

    [Test]
    public void None_Is_Also_The_Null_Of_A_Reference_Type()
    {
        var parsers = Make();
        parsers.Register(new TextureParser());

        var none = Stream("none");
        Assert.That(parsers.TryParse<Texture>(ref none, out var nothing), Is.True);
        Assert.That(nothing, Is.Null);

        var texture = Stream("texture(\"a\")");
        Assert.That(parsers.TryParse<Texture>(ref texture, out var found), Is.True);
        Assert.That(found, Is.Not.Null);

        var other = Stream("1");
        Assert.That(parsers.TryParse<Texture>(ref other, out _), Is.False);
    }
}
