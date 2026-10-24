#nullable enable
using Content.Client._RF.Stylesheets;
using Content.Client._RF.Suil;
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

    [Test]
    public void Palette_Matches_The_Legacy_Style()
    {
        Assert.That(SuilPalette.GoldFortress, Is.EqualTo(StyleFortress.GoldFortress));
        Assert.That(SuilPalette.GraySilver, Is.EqualTo(StyleFortress.GraySilver));
        Assert.That(SuilPalette.LightGood, Is.EqualTo(StyleFortress.LightGood));
        Assert.That(SuilPalette.GoldButtonColorHovered, Is.EqualTo(StyleFortress.GoldButtonColorHovered));
    }
}
