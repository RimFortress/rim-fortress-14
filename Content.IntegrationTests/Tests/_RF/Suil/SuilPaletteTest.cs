#nullable enable
using System.Collections.Generic;
using Content.Client._RF.Suil;
using Content.Suil;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._RF.Suil;

/// <summary>
/// <see cref="SuilPalette"/>: the colors of <c>/variables.suil</c> for C#.
/// </summary>
[TestFixture]
public sealed class SuilPaletteTest
{
    [Test]
    public void Palette_Has_The_Colors_Of_The_Theme()
    {
        Assert.That(SuilPalette.GoldFortress, Is.EqualTo(Color.FromHex("#B07D2B")));
        Assert.That(SuilPalette.LightBad, Is.EqualTo(Color.FromHex("#BF2233")));
        Assert.That(SuilPalette.ScrollbarGrabberDefault, Is.EqualTo(Color.FromHex("#5E4B3A59")), "with alpha");
        Assert.That(SuilPalette.TreeMenuButtonColorDefault, Is.EqualTo(Color.Transparent), "transparent");
    }

    // What the generator writes for the colors that are not a hex number: the parser of the stylesheets reads them.
    [Test]
    public void Parser_Reads_Names_And_Functions_Like_The_Stylesheets()
    {
        Assert.That(SuilPaletteParser.Parse("n", new SuilIdent("red")), Is.EqualTo(Color.Red));
        Assert.That(SuilPaletteParser.Parse("n", new SuilIdent("Transparent")), Is.EqualTo(Color.Transparent));

        var rgba = new SuilFunction("rgba", new List<List<ISuilValue>>
        {
            new() { new SuilNumber(10, true) },
            new() { new SuilNumber(20, true) },
            new() { new SuilNumber(30, true) },
            new() { new SuilNumber(0.5, false) },
        });
        Assert.That(SuilPaletteParser.Parse("n", rgba), Is.EqualTo(new Color(10, 20, 30, 127)));

        var mix = new SuilFunction("color-mix", new List<List<ISuilValue>>
        {
            new() { new SuilIdent("in"), new SuilIdent("srgb") },
            new() { new SuilIdent("red"), new SuilPercentage(25) },
            new() { new SuilIdent("transparent") },
        });
        Assert.That(SuilPaletteParser.Parse("n", mix).A, Is.EqualTo(0.25f).Within(0.01f));

        Assert.Throws<InvalidOperationException>(() => SuilPaletteParser.Parse("n", new SuilIdent("row")));
    }
}
