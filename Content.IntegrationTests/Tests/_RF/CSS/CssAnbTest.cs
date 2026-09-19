using System.Linq;
using Content.Client._RF.CSS;

namespace Content.IntegrationTests.Tests._RF.CSS;

[TestFixture, TestOf(typeof(CssAnb))]
public sealed class CssAnbTest
{
    [TestCase(2, 1, 1, true)]  // odd: 1,3,5,7...
    [TestCase(2, 1, 2, false)]
    [TestCase(2, 1, 7, true)]
    [TestCase(2, 0, 2, true)]  // even: 2,4,6...
    [TestCase(2, 0, 1, false)]
    [TestCase(2, 0, 7, false)]
    [TestCase(0, 5, 5, true)]  // "5" alone: only position 5
    [TestCase(0, 5, 4, false)]
    [TestCase(0, 5, 6, false)]
    [TestCase(3, 1, 1, true)]  // 3n+1: 1,4,7,10...
    [TestCase(3, 1, 4, true)]
    [TestCase(3, 1, 3, false)]
    [TestCase(-1, 3, 1, true)] // -n+3: 3,2,1 (n=2,1,0)
    [TestCase(-1, 3, 2, true)]
    [TestCase(-1, 3, 3, true)]
    [TestCase(-1, 3, 4, false)] // would need n=-1, rejected
    [TestCase(-1, 3, 0, false)] // position 0 is invalid, but check formula doesn't false-positive
    public void Matches_Follows_The_An_Plus_B_Formula(int a, int b, int position, bool expected)
    {
        var anb = new CssAnb(a, b);

        Assert.That(anb.Matches(position), Is.EqualTo(expected));
    }

    [Test]
    public void Zero_A_Never_Matches_Anything_Except_Exact_B()
    {
        var anb = new CssAnb(0, 3);

        for (var p = 1; p <= 10; p++)
        {
            Assert.That(anb.Matches(p), Is.EqualTo(p == 3), $"position {p}");
        }
    }

    [Test]
    public void Negative_A_Produces_A_Bounded_Descending_Sequence()
    {
        // -2n+5: 5,3,1 (n=0,1,2), then n=3 would give -1, rejected
        var anb = new CssAnb(-2, 5);
        var matches = Enumerable.Range(1, 10).Where(anb.Matches).ToArray();

        Assert.That(matches, Is.EqualTo([1, 3, 5]));
    }
}

