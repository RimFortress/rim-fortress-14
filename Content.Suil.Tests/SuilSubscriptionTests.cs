using System.Linq;

namespace Content.Suil.Tests;

[TestFixture]
public sealed class SuilSubscriptionTests
{
    private sealed class Source(Dictionary<string, string> files) : ISuilFileSource
    {
        public SuilFile Load(string path)
            => SuilParser.ParseFile(files.TryGetValue(path, out var text) ? text : throw new FileNotFoundException(path), path);

        public IReadOnlyList<string> List(string directory) => Array.Empty<string>();
    }

    [Test]
    public void A_Subscription_Names_The_Event_The_View_And_What_To_Do()
    {
        var file = SuilParser.ParseFile("""
            @Hud as hud => box

            on @Systems.Hunger.Changed for hud => {
                #bar.value: args.value;
                @Refresh
            }

            on @Controllers.Info.Said for hud => @Show
            on @Systems.Hunger.Reset for hud => #bar.value: 0;
            """);

        var subscriptions = file.Subscriptions.ToList();
        Assert.That(subscriptions, Has.Count.EqualTo(3));

        var first = subscriptions[0];
        Assert.That(first.Scope, Is.EqualTo(SuilCallScope.System));
        Assert.That(first.Name, Is.EqualTo("Hunger"));
        Assert.That(first.Event, Is.EqualTo("Changed"));
        Assert.That(first.Tag, Is.EqualTo("hud"));
        Assert.That(first.Handler.IsBlock, Is.True);
        Assert.That(first.Handler.Statements, Has.Count.EqualTo(2));

        Assert.That(subscriptions[1].Scope, Is.EqualTo(SuilCallScope.Controller));
        Assert.That(subscriptions[1].Handler.IsBlock, Is.False);
        Assert.That(subscriptions[2].Handler.Statements.Single(), Is.InstanceOf<SuilTargetStatement>());
    }

    [TestCase("on @Hunger.Changed for hud => @Go")]
    [TestCase("on @Other.Hunger.Changed for hud => @Go")]
    [TestCase("on @Systems.Hunger.Changed hud => @Go")]
    [TestCase("on @Systems.Hunger.Changed for => @Go")]
    [TestCase("on @Systems.Hunger.Changed for _ => @Go")]
    [TestCase("on @Systems.Hunger.Changed for hud @Go")]
    [TestCase("on @Systems.Hunger.Changed for hud => ")]
    [TestCase("on @Systems.Hunger.Changed for hud => { }")]
    public void A_Broken_Subscription_Is_An_Error(string text)
    {
        Assert.Throws<SuilParserError>(() => SuilParser.ParseFile(text));
    }

    [Test]
    public void The_Subscriptions_Of_An_Imported_File_Are_Not_Brought_In()
    {
        var source = new Source(new Dictionary<string, string>
        {
            ["/lib.suil"] = "@Lib as lib => box\non @Systems.Hunger.Changed for lib => @Go\n",
        });

        var file = SuilExpander.Expand("use \"/lib\";\n@Hud as hud => box\non @Systems.Hunger.Changed for hud => @Go\n", source);

        Assert.That(file.Subscriptions.Select(s => s.Tag), Is.EqualTo(new[] { "hud" }));
    }

    [Test]
    public void A_Binding_Is_Per_Frame_Every_So_Often_Or_On_An_Event()
    {
        var root = SuilParser.ParseFile("""
            box {
                _ (
                    text: bind str(@Systems.Hunger.Get()),
                    min-width: bind #a.min-width every 250ms,
                    min-height: bind = if (#a.disabled) 1 else 2 every 2s,
                    visible: bind !#a.disabled on @Controllers.Info.Said,
                    padding: 4
                )
            }
            """).Root!;

        var properties = ((SuilElement) root.Children.Single()).Properties;
        var binds = properties.Select(p => p.Value is [SuilBindValue b] ? b : null).ToList();

        Assert.That(binds[0]!.Mode, Is.EqualTo(SuilBindMode.Frame));
        Assert.That(binds[1]!.Mode, Is.EqualTo(SuilBindMode.Every));
        Assert.That(binds[1]!.Seconds, Is.EqualTo(0.25).Within(1e-9));
        Assert.That(binds[2]!.Seconds, Is.EqualTo(2));
        Assert.That(binds[3]!.Mode, Is.EqualTo(SuilBindMode.Event));
        Assert.That(binds[3]!.Scope, Is.EqualTo(SuilCallScope.Controller));
        Assert.That(binds[3]!.Name, Is.EqualTo("Info"));
        Assert.That(binds[3]!.Event, Is.EqualTo("Said"));
        Assert.That(binds[4], Is.Null, "an ordinary property");
    }

    [TestCase("box (text: bind every 1s)")]
    [TestCase("box (text: bind 5 every 0s)")]
    [TestCase("box (text: bind 5 every 3)")]
    [TestCase("box (text: bind 5 every 3m)")]
    [TestCase("box (text: bind 5 on Hunger.Changed)")]
    [TestCase("box (text: bind 5 on @Hunger.Changed)")]
    [TestCase("box (text: bind 5 on @Systems.Hunger)")]
    public void A_Broken_Binding_Is_An_Error(string text)
    {
        Assert.Throws<SuilParserError>(() => SuilParser.ParseFile(text));
    }
}
