using System.Globalization;

namespace Content.Suil.Tests;

[TestFixture]
public sealed class SuilTextTests
{
    [Test]
    public void A_Value_Is_A_Text_In_The_Invariant_Culture()
    {
        var saved = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("ru-RU");

        try
        {
            Assert.That(SuilText.ToText(2.5), Is.EqualTo("2.5"));
            Assert.That(SuilText.ToText(3.0), Is.EqualTo("3"));
            Assert.That(SuilText.ToText(1.5f), Is.EqualTo("1.5"));
            Assert.That(SuilText.ToText(42), Is.EqualTo("42"));
            Assert.That(SuilText.ToText(true), Is.EqualTo("true"));
            Assert.That(SuilText.ToText(null), Is.EqualTo(""));
            Assert.That(SuilText.ToText("x"), Is.EqualTo("x"));
            Assert.That(SuilText.ToText(DayOfWeek.Monday), Is.EqualTo("Monday"));
            Assert.That(SuilText.ParseNumber("1.5"), Is.EqualTo(1.5));
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
        }
    }

    [Test]
    public void A_Text_That_Is_Not_A_Number_Is_Zero()
    {
        Assert.That(SuilText.ParseNumber("abc"), Is.EqualTo(0));
        Assert.That(SuilText.ParseNumber(null), Is.EqualTo(0));
        Assert.That(SuilText.ParseNumber(""), Is.EqualTo(0));
        Assert.That(SuilText.ParseNumber("-3"), Is.EqualTo(-3));
    }
}
