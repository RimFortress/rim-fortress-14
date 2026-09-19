using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Client._RF.CSS;

namespace Content.IntegrationTests.Tests._RF.CSS;

[TestFixture, TestOf(typeof(CssParser))]
public sealed class CssAnbParserTest
{
    private static bool TryParse(string input, [NotNullWhen(true)] out CssAnb? result)
    {
        var tokens = CssTokenizer.Tokenize(input).Cast<ICssComponentValue>().ToList();
        return CssParser.TryParseAnBMicrosyntax(tokens, out result);
    }

    // ── Keywords ──────────────────────────────────────────────────────

    [Test]
    public void Odd_Parses_As_2n_Plus_1()
    {
        Assert.That(TryParse("odd", out var result), Is.True);
        Assert.That(result, Is.EqualTo(new CssAnb(2, 1)));
    }

    [Test]
    public void Even_Parses_As_2n_Plus_0()
    {
        Assert.That(TryParse("even", out var result), Is.True);
        Assert.That(result, Is.EqualTo(new CssAnb(2, 0)));
    }

    // ── Bare integer (A=0) ───────────────────────────────────────────

    [TestCase("5", 0, 5)]
    [TestCase("0", 0, 0)]
    [TestCase("-5", 0, -5)] // tokenizer fuses the leading '-' directly into the number
    public void Bare_Integer_Parses_As_A_Zero(string input, int expectedA, int expectedB)
    {
        Assert.That(TryParse(input, out var result), Is.True);
        Assert.That(result, Is.EqualTo(new CssAnb(expectedA, expectedB)));
    }

    // ── "n" with just a coefficient, B=0 ─────────────────────────────

    [TestCase("n", 1, 0)]
    [TestCase("-n", -1, 0)]
    [TestCase("3n", 3, 0)]
    [TestCase("-3n", -3, 0)]
    public void N_With_No_Offset_Parses_With_B_Zero(string input, int expectedA, int expectedB)
    {
        Assert.That(TryParse(input, out var result), Is.True);
        Assert.That(result, Is.EqualTo(new CssAnb(expectedA, expectedB)));
    }

    // ── Fused offset: tokenizer glues sign+digit into the number token ──

    [TestCase("n+3", 1, 3)]
    [TestCase("n-3", 1, -3)]   // fuses into one IdentToken "n-3" at the tokenizer level
    [TestCase("3n+1", 3, 1)]
    [TestCase("3n-1", 3, -1)]  // fuses into one DimensionToken(3, "n-1")
    [TestCase("-n+3", -1, 3)]
    [TestCase("-n-3", -1, -3)] // fuses into one IdentToken "-n-3"
    [TestCase("-3n+1", -3, 1)]
    [TestCase("-3n-1", -3, -1)]
    public void Fused_Sign_And_Offset_Parse_Correctly(string input, int expectedA, int expectedB)
    {
        Assert.That(TryParse(input, out var result), Is.True, $"failed to parse '{input}'");
        Assert.That(result, Is.EqualTo(new CssAnb(expectedA, expectedB)));
    }

    // ── Spaced offset: separate '+'/'-' delim token and a signless integer ──

    [TestCase("n + 3", 1, 3)]
    [TestCase("n - 3", 1, -3)]
    [TestCase("3n + 1", 3, 1)]
    [TestCase("3n - 1", 3, -1)]
    public void Spaced_Sign_And_Offset_Parse_Correctly(string input, int expectedA, int expectedB)
    {
        Assert.That(TryParse(input, out var result), Is.True, $"failed to parse '{input}'");
        Assert.That(result, Is.EqualTo(new CssAnb(expectedA, expectedB)));
    }

    // ── "n- N" form (unit "n-" split from the digits by whitespace) ──

    [TestCase("n- 3", 1, -3)]
    [TestCase("3n- 1", 3, -1)]
    [TestCase("-n- 3", -1, -3)]
    public void Dash_Unit_Followed_By_Separate_Digits_Parses_Correctly(string input, int expectedA, int expectedB)
    {
        Assert.That(TryParse(input, out var result), Is.True, $"failed to parse '{input}'");
        Assert.That(result, Is.EqualTo(new CssAnb(expectedA, expectedB)));
    }

    // ── Leading unary '+' (only meaningful before a bare "n", since '+'
    // immediately before a digit is already fused into the number by the
    // tokenizer and never reaches CssAnbParser as a separate delim token) ──

    [TestCase("+n", 1, 0)]
    [TestCase("+n-1", 1, -1)]
    [TestCase("+n+1", 1, 1)]
    public void Leading_Unary_Plus_Before_Bare_N_Parses_Correctly(string input, int expectedA, int expectedB)
    {
        Assert.That(TryParse(input, out var result), Is.True, $"failed to parse '{input}'");
        Assert.That(result, Is.EqualTo(new CssAnb(expectedA, expectedB)));
    }

    // ── Invalid input ──────────────────────────────────────────────

    [TestCase("")]
    [TestCase("foo")]
    [TestCase("1.5")]        // a fractional number isn't a valid An+B integer
    [TestCase("n n")]        // trailing garbage after a complete match
    [TestCase("n+")]         // dangling sign with no following integer
    [TestCase("n+ n")]       // offset must be a number, not another ident
    [TestCase("3n+1 2")]     // trailing token after a complete match
    public void Invalid_Input_Fails_To_Parse(string input)
    {
        Assert.That(TryParse(input, out _), Is.False, $"'{input}' should not have parsed");
    }
}
