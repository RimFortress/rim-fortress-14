using System.Collections.Generic;
using System.Linq;
using Content.Client._RF.CSS;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Robust.Shared.Reflection;

// ReSharper disable ReturnValueOfPureMethodIsNotUsed

namespace Content.IntegrationTests.Tests._RF.CSS;

[TestFixture, TestOf(typeof(CssParser))]
public sealed class CssParserTests : GameTest
{
    [SidedDependency(Side.Client)] private readonly IReflectionManager _reflection = default!;

    [Test]
    public void Simple_Declaration_Is_Parsed_With_Correct_Name_And_Value()
    {
        var result = CssParser.ParseListOfDeclarations("color: red;");
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0], Is.TypeOf<CssDeclaration>());

        var decl = (CssDeclaration)result[0];
        Assert.That(decl.Name, Is.EqualTo("color"));
        Assert.That(decl.Important, Is.False);
        Assert.That(decl.Value, Has.Count.EqualTo(1));
        Assert.That(decl.Value[0], Is.EqualTo(new IdentToken("red")));
    }

    [Test]
    public void Declaration_With_Multiple_Value_Tokens_Keeps_Them_In_Order()
    {
        var result = CssParser.ParseListOfDeclarations("margin: 10px 5px;");

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0], Is.TypeOf<CssDeclaration>());
        var decl = (CssDeclaration)result[0];

        Assert.That(decl.Name, Is.EqualTo("margin"));
        Assert.That(
            decl.Value,
            Is.EqualTo(new List<ICssComponentValue>
            {
                new DimensionToken(10, true, "px"),
                new WhitespaceToken(),
                new DimensionToken(5, true, "px"),
            }));
    }

    [Test]
    public void Important_Flag_Is_Detected_And_Stripped_From_Value()
    {
        var result = CssParser.ParseListOfDeclarations("color: red !important;");
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0], Is.TypeOf<CssDeclaration>());

        var decl = (CssDeclaration)result[0];
        Assert.That(decl.Important, Is.True);
        // only the ident token should remain — "!important" must be gone
        Assert.That(decl.Value, Has.Count.EqualTo(1));
        Assert.That(decl.Value[0], Is.EqualTo(new IdentToken("red")));
    }

    [Test]
    public void Important_Case_Insensitive_Match_Is_Honored()
    {
        var result = CssParser.ParseListOfDeclarations("color: red !IMPORTANT;");
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0], Is.TypeOf<CssDeclaration>());

        var decl = (CssDeclaration)result[0];
        Assert.That(decl.Important, Is.True);
    }

    [Test]
    public void Trailing_Whitespace_In_Declaration_Value_Is_Trimmed()
    {
        var result = CssParser.ParseListOfDeclarations("color: red   ;");
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0], Is.TypeOf<CssDeclaration>());

        var decl = (CssDeclaration)result[0];
        Assert.That(decl.Value, Has.Count.EqualTo(1));
        Assert.That(decl.Value[0], Is.EqualTo(new IdentToken("red")));
    }

    [Test]
    public void Function_In_Declaration_Value_Is_Folded_Into_A_Single_CssFunction()
    {
        var result = CssParser.ParseListOfDeclarations("color: rgb(255, 0, 0);");

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0], Is.TypeOf<CssDeclaration>());
        var decl = (CssDeclaration)result[0];

        Assert.That(decl.Value, Has.Count.EqualTo(1));
        Assert.That(decl.Value[0], Is.TypeOf<CssFunction>());
        var fn = (CssFunction)decl.Value[0];

        Assert.That(fn.Name, Is.EqualTo("rgb"));
        Assert.That(
            fn.Value,
            Is.EqualTo(new List<ICssComponentValue>
            {
                new NumberToken(255, true),
                new CommaToken(),
                new WhitespaceToken(),
                new NumberToken(0, true),
                new CommaToken(),
                new WhitespaceToken(),
                new NumberToken(0, true),
            }));
    }

    [Test]
    public void Multiple_Declarations_Are_All_Parsed_In_Order()
    {
        var result = CssParser.ParseListOfDeclarations("color: red; background: blue;");

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(((CssDeclaration)result[0]).Name, Is.EqualTo("color"));
        Assert.That(((CssDeclaration)result[1]).Name, Is.EqualTo("background"));
    }

    [Test]
    public void At_Rule_Inside_Declaration_List_Is_Collected_Separately()
    {
        var result = CssParser.ParseListOfDeclarations("color: red; @unknown foo;");

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result[0], Is.TypeOf<CssDeclaration>());

        Assert.That(result[1], Is.TypeOf<CssAtRule>());
        var atRule = (CssAtRule)result[1];
        Assert.That(atRule.Name, Is.EqualTo("unknown"));
        Assert.That(atRule.Block, Is.Null);
    }

    [Test]
    public void Missing_Colon_In_Declaration_Throws()
    {
        Assert.Throws<CssParserError>(() => CssParser.ParseListOfDeclarations("color red;"));
    }

    [Test]
    public void Qualified_Rule_Prelude_And_Block_Are_Split_Correctly()
    {
        var rules = CssParser.ParseStylesheet("label.title { color: red; }");

        Assert.That(rules, Has.Count.EqualTo(1));
        Assert.That(rules[0], Is.TypeOf<CssQualifiedRule>());
        var rule = (CssQualifiedRule)rules[0];

        // prelude should be exactly the selector tokens, unparsed
        Assert.That(
            rule.Prelude,
            Is.EqualTo(new List<ICssComponentValue>
            {
                new IdentToken("Label"),
                new DelimToken('.'),
                new IdentToken("title"),
                new WhitespaceToken(),
            }));

        Assert.That(rule.Block, Is.Not.Null);
        Assert.That(rule.Block.AssociatedToken, Is.TypeOf<LeftCurlyBracket>());
    }

    [Test]
    public void Qualified_Rule_Block_Contents_Parse_As_Declarations()
    {
        var rules = CssParser.ParseStylesheet("button { color: red; }");
        Assert.That(rules, Has.Count.EqualTo(1));
        Assert.That(rules[0], Is.TypeOf<CssQualifiedRule>());
        var rule = (CssQualifiedRule)rules[0];

        var declarations = CssParser.ParseListOfDeclarations(rule.Block!.Value.ToArray());

        Assert.That(declarations, Has.Count.EqualTo(1));
        Assert.That(declarations[0], Is.TypeOf<CssDeclaration>());
        var decl = (CssDeclaration)declarations[0];
        Assert.That(decl.Name, Is.EqualTo("color"));
    }

    [Test]
    public void At_Rule_Without_Block_Ends_At_Semicolon()
    {
        var rules = CssParser.ParseStylesheet("@import \"foo.css\";");

        Assert.That(rules, Has.Count.EqualTo(1));
        Assert.That(rules[0], Is.TypeOf<CssAtRule>());
        var atRule = (CssAtRule)rules[0];

        Assert.That(atRule.Name, Is.EqualTo("import"));
        Assert.That(atRule.Block, Is.Null);
        Assert.That(
            atRule.Prelude,
            Is.EqualTo(new List<ICssComponentValue>
            {
                new WhitespaceToken(),
                new StringToken("foo.css"),
            }));
    }

    [Test]
    public void At_Rule_With_Block_Captures_Nested_Content_As_A_Simple_Block()
    {
        var rules = CssParser.ParseStylesheet("@media screen { button { color: red; } }");

        Assert.That(rules, Has.Count.EqualTo(1));
        Assert.That(rules[0], Is.TypeOf<CssAtRule>());
        var atRule = (CssAtRule)rules[0];

        Assert.That(atRule.Name, Is.EqualTo("media"));
        Assert.That(atRule.Block, Is.Not.Null);

        // nested rule should be reachable by feeding the block's contents back into the parser
        var nested = CssParser.ParseStylesheet(atRule.Block!.Value.ToArray());

        Assert.That(nested, Has.Count.EqualTo(1));
        Assert.That(nested[0], Is.TypeOf<CssQualifiedRule>());
        var nestedRule = (CssQualifiedRule)nested[0];
        Assert.That(nestedRule.Block, Is.Not.Null);
    }

    [Test]
    public void Multiple_Top_Level_Rules_Are_All_Returned_In_Order()
    {
        var rules = CssParser.ParseStylesheet("a { color: red; } b { color: blue; }");

        Assert.That(rules, Has.Count.EqualTo(2));
        Assert.That(rules.All(r => r is CssQualifiedRule), Is.True);
    }

    [Test]
    public void Unclosed_Block_Throws()
    {
        Assert.Throws<CssParserError>(() => CssParser.ParseStylesheet("a { color: red;"));
    }

    [Test]
    public void Unclosed_Function_Throws()
    {
        Assert.Throws<CssParserError>(() => CssParser.ParseStylesheet("a { color: rgb(255, 0, 0; }"));
    }

    [Test]
    [TestCase(":focus {}", typeof(FocusSelector))]
    [TestCase(":root {}", typeof(RootSelector))]
    [TestCase(":empty {}", typeof(EmptySelector))]
    [TestCase(":first-child {}", typeof(NthChildSelector))]
    [TestCase(":last-child {}", typeof(NthLastChildSelector))]
    [TestCase(":first-of-type {}", typeof(NthOfTypeSelector))]
    [TestCase(":last-of-type {}", typeof(NthLastOfTypeSelector))]
    [TestCase(":only-child {}", typeof(OnlyChildSelector))]
    [TestCase(":only-of-type {}", typeof(OnlyOfTypeSelector))]
    [TestCase(":nth-child(2n) {}", typeof(NthChildSelector))]
    [TestCase(":nth-of-type(even) {}", typeof(NthOfTypeSelector))]
    [TestCase(":nth-last-child(3n + 1) {}", typeof(NthLastChildSelector))]
    [TestCase(":nth-last-of-type(odd) {}", typeof(NthLastOfTypeSelector))]
    [RunOnSide(Side.Client)]
    public void SelectorsParseTest(string text, Type type)
    {
        var result = CssParser.ParseStylesheet(text);
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0], Is.TypeOf<CssQualifiedRule>());

        var qualified = (CssQualifiedRule)result[0];
        Assert.That(CssParser.TryGetSelectors(qualified, _reflection, out var selectors), Is.True);
        Assert.That(selectors, Has.Count.EqualTo(1));
        Assert.That(selectors![0], Is.TypeOf(type));
    }

    [TestCase(":not(:first-child) {}", typeof(NotSelector), typeof(NthChildSelector))]
    [TestCase(":not(:focus, :root) {}", typeof(NotSelector), typeof(FocusSelector), typeof(RootSelector))]
    [TestCase(":not(:empty, :nth-child(2n)) {}", typeof(NotSelector), typeof(EmptySelector), typeof(NthChildSelector))]
    [TestCase(":is(:last-of-type, :nth-last-child(5n - 3)) {}", typeof(OrSelector), typeof(NthLastOfTypeSelector), typeof(NthLastChildSelector))]
    [TestCase(":is(:empty, :nth-of-type(even)) {}", typeof(OrSelector), typeof(EmptySelector), typeof(NthOfTypeSelector))]
    [TestCase(":is(:nth-last-of-type(odd), :first-of-type, :only-of-type) {}", typeof(OrSelector), typeof(NthLastOfTypeSelector), typeof(NthOfTypeSelector), typeof(OnlyOfTypeSelector))]
    [RunOnSide(Side.Client)]
    public void CompoundSelectorsParseTest(string text, Type compoundType, params Type[] type)
    {
        var result = CssParser.ParseStylesheet(text);
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0], Is.TypeOf<CssQualifiedRule>());

        var qualified = (CssQualifiedRule)result[0];
        Assert.That(CssParser.TryGetSelectors(qualified, _reflection, out var selectors), Is.True);
        Assert.That(selectors, Has.Count.EqualTo(1));
        Assert.That(selectors![0], Is.TypeOf(compoundType));

        var main = (CompoundSelector)selectors[0];

        for (var i = 0; i < type.Length; i++)
        {
            Assert.That(main.Selectors[i],
                Is.TypeOf(type[i]),
                $"expected {type[i].FullName}, not {main.Selectors[i].GetType().FullName}");
        }
    }
}
