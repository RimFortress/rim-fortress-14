using Content.Suil;

namespace Content.Suil.Tests;

[TestFixture]
public sealed class SuilSelectorTests
{
    private static SuilCompoundSelector Compound(string selector)
    {
        var rule = (SuilRule) SuilParser.ParseFile($"{selector} {{ x: 1; }}").Items.Single();
        return rule.Selectors.Single().Steps.Single().Compound;
    }

    private static SuilSelector Selector(string selector)
        => ((SuilRule) SuilParser.ParseFile($"{selector} {{ x: 1; }}").Items.Single()).Selectors.Single();

    // ---- Simple parts ----

    [Test]
    public void Universal_Selector()
    {
        var compound = Compound("*");

        Assert.That(compound.Universal, Is.True);
        Assert.That(compound.Type, Is.Null);

        var withClass = Compound("*.a:hover");
        Assert.That(withClass.Universal, Is.True);
        Assert.That(withClass.Classes, Is.EqualTo(new[] { "a" }));
        Assert.That(withClass.PseudoClasses, Is.EqualTo(new[] { "hover" }));
    }

    [Test]
    public void Universal_Selector_In_A_Chain()
    {
        var steps = Selector("* > .a *").Steps;

        Assert.That(steps.Select(s => s.Combinator), Is.EqualTo(new[] { SuilCombinator.None, SuilCombinator.Child, SuilCombinator.Descendant }));
        Assert.That(steps[0].Compound.Universal, Is.True);
        Assert.That(steps[2].Compound.Universal, Is.True);
    }

    [Test]
    public void Structural_Pseudo_Classes_Are_Names()
    {
        Assert.That(Compound(":root").PseudoClasses, Is.EqualTo(new[] { "root" }));
        Assert.That(Compound("button:first-child:only-of-type").PseudoClasses, Is.EqualTo(new[] { "first-child", "only-of-type" }));
    }

    // ---- Attributes ----

    [TestCase("[x]", SuilAttributeOperator.Exists)]
    [TestCase("[x=1]", SuilAttributeOperator.Equals)]
    [TestCase("[x*=\"a\"]", SuilAttributeOperator.Contains)]
    [TestCase("[x^=\"a\"]", SuilAttributeOperator.StartsWith)]
    [TestCase("[x$=\"a\"]", SuilAttributeOperator.EndsWith)]
    [TestCase("[x~=\"a\"]", SuilAttributeOperator.WhitespaceList)]
    [TestCase("[x|=\"a\"]", SuilAttributeOperator.HyphenList)]
    [TestCase("[ x = 1 ]", SuilAttributeOperator.Equals)]
    public void Attribute_Operators(string selector, SuilAttributeOperator expected)
    {
        var attribute = Compound($"button{selector}").Attributes.Single();

        Assert.That(attribute.Name, Is.EqualTo("x"));
        Assert.That(attribute.Operator, Is.EqualTo(expected));
        Assert.That(attribute.IgnoreCase, Is.False);
    }

    [Test]
    public void Attribute_Values_And_Flags()
    {
        var plain = Compound("[locale-text=\"test\"]").Attributes.Single();
        Assert.That(plain.Value.Single(), Is.EqualTo(new SuilString(System.Collections.Immutable.ImmutableArray.Create(StringPart.Literal("test")))));

        var number = Compound("[min-width=12px]").Attributes.Single();
        Assert.That(number.Value.Single(), Is.EqualTo(new SuilDimension(12, true, "px")));

        var several = Compound("[padding=1px 2px]").Attributes.Single();
        Assert.That(several.Value, Has.Count.EqualTo(2));

        var color = Compound("[color=#B07D2B]").Attributes.Single();
        Assert.That(color.Value.Single(), Is.EqualTo(new SuilColor("B07D2B")));

        var insensitive = Compound("[x=\"a\" i]").Attributes.Single();
        Assert.That(insensitive.IgnoreCase, Is.True);
        Assert.That(insensitive.Value, Has.Count.EqualTo(1));

        var sensitive = Compound("[x=\"a\" S]").Attributes.Single();
        Assert.That(sensitive.IgnoreCase, Is.False);
        Assert.That(sensitive.Value, Has.Count.EqualTo(1));

        // a lone `i` is the value, not a flag
        var lone = Compound("[x=i]").Attributes.Single();
        Assert.That(lone.Value.Single(), Is.EqualTo(new SuilIdent("i")));
    }

    [Test]
    public void The_Property_May_Be_A_String_And_Several_Attributes_Are_Allowed()
    {
        var compound = Compound("button[\"x\"=1][y][z=2]");

        Assert.That(compound.Attributes.Select(a => a.Name), Is.EqualTo(new[] { "x", "y", "z" }));
    }

    [Test]
    public void Attribute_Next_To_A_Class_And_After_A_Space()
    {
        Assert.That(Selector(".a[x=1]").Steps, Has.Count.EqualTo(1));
        Assert.That(Selector(".a [x=1]").Steps, Has.Count.EqualTo(2), "a space is the descendant combinator");
    }

    // ---- Functions ----

    [TestCase("odd", 2, 1)]
    [TestCase("even", 2, 0)]
    [TestCase("3", 0, 3)]
    [TestCase("2n+1", 2, 1)]
    [TestCase("2n + 1", 2, 1)]
    [TestCase("2n-1", 2, -1)]
    [TestCase("2n - 1", 2, -1)]
    [TestCase("n", 1, 0)]
    [TestCase("-n+3", -1, 3)]
    [TestCase("+n", 1, 0)]
    [TestCase("5n - 3", 5, -3)]
    [TestCase("n-2", 1, -2)]
    [TestCase("-n-2", -1, -2)]
    public void Nth_Child_Takes_An_Anb(string anb, int a, int b)
    {
        var function = Compound($":nth-child({anb})").Functions.Single();

        Assert.That(function.Name, Is.EqualTo("nth-child"));
        Assert.That(function.Anb, Is.EqualTo(new SuilAnb(a, b)));
    }

    [Test]
    public void All_Nth_Functions()
    {
        var names = Compound("a:nth-child(1):nth-last-child(2):nth-of-type(3):nth-last-of-type(4)")
            .Functions.Select(f => f.Name);

        Assert.That(names, Is.EqualTo(new[] { "nth-child", "nth-last-child", "nth-of-type", "nth-last-of-type" }));
    }

    [Test]
    public void Anb_Matches_Positions()
    {
        var odd = new SuilAnb(2, 1);

        Assert.That(new[] { 1, 2, 3, 4, 5 }.Where(odd.Matches), Is.EqualTo(new[] { 1, 3, 5 }));
        Assert.That(new SuilAnb(-1, 3).Matches(1), Is.True);
        Assert.That(new SuilAnb(-1, 3).Matches(4), Is.False);
        Assert.That(new SuilAnb(0, 2).Matches(2), Is.True);
        Assert.That(new SuilAnb(1, 0).Matches(0), Is.False);
    }

    [Test]
    public void Not_And_Is_Take_Selector_Lists()
    {
        var not = Compound(":not(.a, button > .b)").Functions.Single();
        var @is = Compound(":is(:empty, :nth-of-type(even))").Functions.Single();

        Assert.That(not.Name, Is.EqualTo("not"));
        Assert.That(not.Selectors, Has.Count.EqualTo(2));
        Assert.That(not.Selectors![0].Steps.Single().Compound.Classes, Is.EqualTo(new[] { "a" }));
        Assert.That(not.Selectors[1].Steps, Has.Count.EqualTo(2));

        Assert.That(@is.Selectors![1].Steps.Single().Compound.Functions.Single().Anb, Is.EqualTo(new SuilAnb(2, 0)));
    }

    [Test]
    public void Functions_Nest()
    {
        var outer = Compound(":not(:is(.a:not(.b)))").Functions.Single();
        var inner = outer.Selectors!.Single().Steps.Single().Compound.Functions.Single();
        var innermost = inner.Selectors!.Single().Steps.Single().Compound.Functions.Single();

        Assert.That(inner.Name, Is.EqualTo("is"));
        Assert.That(innermost.Name, Is.EqualTo("not"));
    }

    [Test]
    public void Functions_Can_Be_On_A_Root_Rule_Together_With_A_Type()
    {
        var file = SuilParser.ParseFile("button:nth-child(odd) { x: 1; } row { _ }");

        Assert.That(file.Items.Single(), Is.TypeOf<SuilRule>());
        Assert.That(file.Root!.Tag, Is.EqualTo("row"));
    }

    [TestCase(":nth-child()")]
    [TestCase(":nth-child(x)")]
    [TestCase(":nth-child(2n+)")]
    [TestCase(":nth-child(1.5)")]
    [TestCase(":unknown(1)")]
    [TestCase(":not(")]
    [TestCase(":not(.a")]
    [TestCase(":not()")]
    [TestCase(":is(.a,)")]
    [TestCase("[]")]
    [TestCase("[x")]
    [TestCase("[x=]")]
    [TestCase("[x!=1]")]
    [TestCase("[x~1]")]
    [TestCase("[=1]")]
    [TestCase("[1=2]")]
    public void Malformed_Selectors_Throw(string selector)
    {
        Assert.Throws<SuilParserError>(() => SuilParser.ParseFile($"button{selector} {{ x: 1; }}"));
    }

    // ---- At-rules and the rest of the front end ----

    [Test]
    public void Font_Face()
    {
        var file = SuilParser.ParseFile("""
            @font-face {
                font-family: "Source Code Pro";
                src: url("/Fonts/A.ttf") format("truetype");
                font-weight: 700;
            }
            .a { x: 1; }
            """);

        var face = (SuilFontFace) file.Items[0];

        Assert.That(face.Descriptors.Select(d => d.Name), Is.EqualTo(new[] { "font-family", "src", "font-weight" }));
        Assert.That(file.Items[1], Is.TypeOf<SuilRule>());
    }

    [Test]
    public void Font_Face_Goes_Through_The_Expander_With_Its_Variables()
    {
        var file = SuilExpander.Expand("""
            let family = "Mono";
            @font-face { font-family: <family>; }
            """);

        var face = (SuilFontFace) file.Items.Single();

        Assert.That(((SuilString) face.Descriptors.Single().Value.Single()).Text, Is.EqualTo("Mono"));
    }

    [TestCase("@media { }")]
    [TestCase("@font-face")]
    [TestCase("@font-face { x }")]
    [TestCase("@")]
    public void Other_At_Rules_Throw(string text)
    {
        Assert.Throws<SuilParserError>(() => SuilParser.ParseFile(text));
    }

    [Test]
    public void Dollar_Equals_Is_An_Operator_And_Not_A_Localization_Key()
    {
        var tokens = SuilTokenizer.Tokenize("$= $key").Select(t => t.Value).ToList();

        Assert.That(tokens[0], Is.EqualTo(new DelimToken('$')));
        Assert.That(tokens[1], Is.EqualTo(new DelimToken('=')));
        Assert.That(tokens[3], Is.TypeOf<LocToken>());
    }
}
