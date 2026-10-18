namespace Content.Suil.Generator.Tests;

/// <summary>
/// <c>on @Systems.X.Event for tag =&gt; ...</c>: the subscriptions of a view to the events of controllers and systems.
/// </summary>
[TestFixture]
public sealed class SuilViewSubscriptionTests
{
    private const string Classes = """
        using System;
        using Content.Suil;
        using Content.Client._RF.Suil;
        using Content.Client._RF.UserInterface.Controls;
        using Robust.Client.UserInterface.Controllers;
        using Robust.Shared.GameObjects;
        namespace Game.Ui;

        public sealed class HungerSystem : EntitySystem
        {
            public event Action<int>? Changed;
            public event Action? Reset;
            public event Func<int>? Wrong;
            public int Value;
            public float Temp { get; set; } = 293.15f;
            public Clock Now => new() { Hours = Value };
            public Clock GetClock() => new() { Hours = Value + 1 };
            public int Get() => Value;
            public int Handlers => Changed?.GetInvocationList().Length ?? 0;
            public void Raise(int value) => Changed?.Invoke(value);
            public void RaiseReset() => Reset?.Invoke();
        }

        public struct Clock { public int Hours { get; set; } }

        public sealed class InfoUiController : UIController
        {
            public event Action<string>? Said;
            public void Say(string text) => Said?.Invoke(text);
        }

        public static class Boot
        {
            public static HungerSystem Hunger = new();
            public static InfoUiController Info = new();

            public static void Setup()
            {
                Robust.Shared.IoC.IoCManager.Register<Robust.Client.UserInterface.IUserInterfaceManager>(new Ui());
                Robust.Shared.IoC.IoCManager.Register<IEntityManager>(new Entities());
            }

            private sealed class Ui : Robust.Client.UserInterface.IUserInterfaceManager
            {
                public T GetUIController<T>() where T : UIController => (T) (object) Info;
            }

            private sealed class Entities : IEntityManager
            {
                public T System<T>() where T : EntitySystem => (T) (object) Hunger;
            }
        }
        """;

    private const string Picker = """
        public sealed partial class Picker : RfButton
        {
            public int Calls;
            public Picker() { InitializeComponent(); }
            private void Count() { Calls++; }
        }
        """;

    private static GeneratorHarness.Result Run(string suil, string picker = Picker)
        => GeneratorHarness.Run(Classes + "\n" + picker, new Dictionary<string, string> { ["/Picker.suil"] = suil });

    private static string[] Ids(GeneratorHarness.Result result) => result.GeneratorDiagnostics.Select(d => d.Id).ToArray();

    private static void AssertClean(GeneratorHarness.Result result)
    {
        Assert.That(result.GeneratorDiagnostics, Is.Empty, string.Join("\n", result.GeneratorDiagnostics));
        Assert.That(result.CompilationErrors, Is.Empty, string.Join("\n", result.CompilationErrors) + "\n" + string.Join("\n", result.Sources.Select(source => source.SourceText.ToString())));
    }

    private const System.Reflection.BindingFlags Any = System.Reflection.BindingFlags.Instance
        | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;

    private static object Get(object target, string name)
        => (target.GetType().GetField(name, Any) as object ?? target.GetType().GetProperty(name, Any)!) switch
        {
            System.Reflection.FieldInfo f => f.GetValue(target)!,
            System.Reflection.PropertyInfo p => p.GetValue(target)!,
            _ => throw new InvalidOperationException(name),
        };

    private static void Call(object target, string method, params object?[] args)
        => target.GetType().GetMethod(method)!.Invoke(target, args);

    [Test]
    public void A_View_Is_Subscribed_While_It_Is_In_The_Tree()
    {
        var result = Run("""
            @Picker as picker => button {
                _ #label (text: "none")
            }

            on @Systems.Hunger.Changed for picker => {
                #label.text: str(args);
                @Count
            }

            on @Systems.Hunger.Reset for picker => #label.text: "reset";
            on @Controllers.Info.Said for picker => #label.text: str(args);
            """);

        AssertClean(result);

        var assembly = result.Load();
        var boot = assembly.GetType("Game.Ui.Boot")!;
        boot.GetMethod("Setup")!.Invoke(null, null);
        var hunger = boot.GetField("Hunger")!.GetValue(null)!;
        var info = boot.GetField("Info")!.GetValue(null)!;

        var view = Activator.CreateInstance(assembly.GetType("Game.Ui.Picker")!)!;
        var label = Get(view, "Label");

        Call(hunger, "Raise", 3);
        Assert.That(Get(view, "Calls"), Is.EqualTo(0), "not in the tree yet");

        Call(view, "EnterTree");
        Call(view, "EnterTree");
        Assert.That(Get(hunger, "Handlers"), Is.EqualTo(1), "the second enter doesn't subscribe again");

        Call(hunger, "Raise", 3);
        Assert.That(Get(view, "Calls"), Is.EqualTo(1));
        Assert.That(label.GetType().GetProperty("Text")!.GetValue(label), Is.EqualTo("3"));

        Call(hunger, "RaiseReset");
        Assert.That(label.GetType().GetProperty("Text")!.GetValue(label), Is.EqualTo("reset"));

        Call(info, "Say", "hi");
        Assert.That(label.GetType().GetProperty("Text")!.GetValue(label), Is.EqualTo("hi"));

        Call(view, "ExitTree");
        Assert.That(Get(hunger, "Handlers"), Is.EqualTo(0));

        Call(hunger, "Raise", 5);
        Assert.That(Get(view, "Calls"), Is.EqualTo(1));

        Call(view, "EnterTree");
        Call(hunger, "Raise", 6);
        Assert.That(Get(view, "Calls"), Is.EqualTo(2), "a view that enters the tree again is subscribed again");
    }

    [TestCase("on @Systems.Nobody.Changed for picker => @Count", "SUIL027")]
    [TestCase("on @Controllers.Nobody.Said for picker => @Count", "SUIL027")]
    [TestCase("on @Systems.Hunger.Missing for picker => @Count", "SUIL029")]
    [TestCase("on @Systems.Hunger.Wrong for picker => @Count", "SUIL030")]
    [TestCase("on @Systems.Hunger.Changed for other => @Count", "SUIL031")]
    public void Mistakes_In_A_Subscription_Are_Reported_At_The_Build(string subscription, string id)
    {
        var result = Run($"@Picker as picker => button\n\n{subscription}\n");

        Assert.That(Ids(result), Has.Member(id), string.Join("\n", result.GeneratorDiagnostics));
    }

    [Test]
    public void A_Class_With_Its_Own_Hooks_Has_To_Pass_Them_On()
    {
        const string suil = """
            @Picker as picker => button

            on @Systems.Hunger.Changed for picker => @Count
            """;

        const string code = """
            public sealed partial class Picker : RfButton
            {
                public int Calls;
                public Picker() { InitializeComponent(); }
                private void Count() { Calls++; }
                protected override void EnteredTree() { base.EnteredTree(); {0} }
                protected override void ExitedTree() { base.ExitedTree(); {1} }
            }
            """;

        var without = Run(suil, code.Replace("{0}", "").Replace("{1}", ""));
        Assert.That(Ids(without), Has.Member("SUIL032"), string.Join("\n", without.GeneratorDiagnostics));

        var passed = Run(suil, code.Replace("{0}", "SuilEnteredTree();").Replace("{1}", "SuilExitedTree();"));
        AssertClean(passed);
    }

    [Test]
    public void A_Binding_Is_Worked_Out_Again_While_The_View_Is_In_The_Tree()
    {
        var result = Run("""
            @Picker as picker => button {
                _ #fast (text: bind str(@Systems.Hunger.Get()))
                _ #slow (text: bind str(@Systems.Hunger.Get()) every 1s)
                _ #event (text: bind str(@Systems.Hunger.Get()) on @Systems.Hunger.Changed)
                _ #cond (min-width: bind if (@Systems.Hunger.Get() > 5) 10 else 1)
            }
            """);

        AssertClean(result);

        var assembly = result.Load();
        var boot = assembly.GetType("Game.Ui.Boot")!;
        boot.GetMethod("Setup")!.Invoke(null, null);
        var hunger = boot.GetField("Hunger")!.GetValue(null)!;
        var value = hunger.GetType().GetField("Value")!;

        string? Text(object view, string field)
        {
            var label = Get(view, field);
            return (string?) label.GetType().GetProperty("Text")!.GetValue(label);
        }

        var view = Activator.CreateInstance(assembly.GetType("Game.Ui.Picker")!)!;
        Assert.That(Text(view, "Fast"), Is.Null, "not in the tree yet");

        value.SetValue(hunger, 3);
        Call(view, "EnterTree");
        Call(view, "EnterTree");
        Assert.That(new[] { Text(view, "Fast"), Text(view, "Slow"), Text(view, "Event") }, Is.All.EqualTo("3"), "worked out when it enters");
        Assert.That(Get(Get(view, "Cond"), "MinWidth"), Is.EqualTo(1));

        value.SetValue(hunger, 7);
        SuilBindings.Update(0.016);
        Assert.That(Text(view, "Fast"), Is.EqualTo("7"), "every frame");
        Assert.That(Text(view, "Slow"), Is.EqualTo("3"), "not yet");
        Assert.That(Text(view, "Event"), Is.EqualTo("3"), "no event");
        Assert.That(Get(Get(view, "Cond"), "MinWidth"), Is.EqualTo(10));

        SuilBindings.Update(1.0);
        Assert.That(Text(view, "Slow"), Is.EqualTo("7"), "after a second");

        value.SetValue(hunger, 8);
        Call(hunger, "Raise", 8);
        Assert.That(Text(view, "Event"), Is.EqualTo("8"), "the event was raised");
        Assert.That(Text(view, "Fast"), Is.EqualTo("7"), "the frame didn't come");

        Call(view, "ExitTree");
        value.SetValue(hunger, 9);
        SuilBindings.Update(5.0);
        Call(hunger, "Raise", 9);
        Assert.That(new[] { Text(view, "Fast"), Text(view, "Slow"), Text(view, "Event") }, Is.EqualTo(new[] { "7", "7", "8" }), "out of the tree it works nothing out");
    }

    [TestCase("@Picker as picker => button { _ #a (text: bind str(@Systems.Nobody.Get())) }", "SUIL027")]
    [TestCase("@Picker as picker => button { _ #a (text: bind str(@Systems.Hunger.Get()) on @Systems.Hunger.Missing) }", "SUIL029")]
    [TestCase("@Picker as picker => button { _ #a (text: bind str(@Systems.Hunger.Get()) on @Systems.Hunger.Wrong) }", "SUIL030")]
    [TestCase("@Picker as picker => button { _ #a (text: bind str(@Systems.Hunger.Get()) on @Systems.Nobody.Changed) }", "SUIL027")]
    [TestCase("@Picker as picker => button { _ #a (nope: bind 5) }", "SUIL010")]
    [TestCase("@Picker as picker => button { _ #a (text: bind str(args)) }", "SUIL023")]
    public void Mistakes_In_A_Binding_Are_Reported_At_The_Build(string suil, string id)
    {
        var result = Run(suil);

        Assert.That(Ids(result), Has.Member(id), string.Join("\n", result.GeneratorDiagnostics));
    }

    [TestCase("@Picker => button\n\non @Systems.Hunger.Changed for picker => @Count")]
    [TestCase("@Picker as tab => button\n\non @Systems.Hunger.Changed for picker => @Count")]
    [TestCase("@Picker as tab => button\n\non @Systems.Hunger.Changed for tab => @Count")]
    public void A_View_Can_Be_Named_By_Its_Default_Tag_Too(string suil)
    {
        var result = Run(suil);

        AssertClean(result);

        var assembly = result.Load();
        var boot = assembly.GetType("Game.Ui.Boot")!;
        boot.GetMethod("Setup")!.Invoke(null, null);
        var hunger = boot.GetField("Hunger")!.GetValue(null)!;

        var view = Activator.CreateInstance(assembly.GetType("Game.Ui.Picker")!)!;
        Call(view, "EnterTree");
        Call(hunger, "Raise", 1);

        Assert.That(Get(view, "Calls"), Is.EqualTo(1));
    }

    [Test]
    public void A_Property_Of_A_System_Or_A_Controller_Can_Be_Read_In_An_Expression()
    {
        var result = Run("""
            @Picker as picker => button {
                _ #temp (text: bind if (@Systems.Hunger.Temp < 283.15) "cold" elif (@Systems.Hunger.Temp < 300) "warm" else "hot")
                _ #hours (text: bind str(@Systems.Hunger.Now.Hours))
                _ #next (text: bind str(@Systems.Hunger.GetClock().Hours))
            }
            """);

        AssertClean(result);

        var assembly = result.Load();
        var boot = assembly.GetType("Game.Ui.Boot")!;
        boot.GetMethod("Setup")!.Invoke(null, null);
        var hunger = boot.GetField("Hunger")!.GetValue(null)!;

        string? Text(object view, string field)
        {
            var label = Get(view, field);
            return (string?) label.GetType().GetProperty("Text")!.GetValue(label);
        }

        hunger.GetType().GetField("Value")!.SetValue(hunger, 7);
        var view = Activator.CreateInstance(assembly.GetType("Game.Ui.Picker")!)!;
        Call(view, "EnterTree");

        Assert.That(Text(view, "Temp"), Is.EqualTo("warm"));
        Assert.That(Text(view, "Hours"), Is.EqualTo("7"));
        Assert.That(Text(view, "Next"), Is.EqualTo("8"));

        hunger.GetType().GetProperty("Temp")!.SetValue(hunger, 270f);
        SuilBindings.Update(0.016);
        Assert.That(Text(view, "Temp"), Is.EqualTo("cold"));
    }

    [TestCase("@Picker as picker => button { _ #a (text: bind str(@Systems.Hunger.Nope)) }")]
    [TestCase("@Picker as picker => button { _ #a (text: bind str(@Systems.Hunger.Now.Nope)) }")]
    [TestCase("@Picker as picker => button { _ #a (text: bind str(@Systems.Hunger.GetClock().Nope)) }")]
    public void A_Member_That_Is_Not_There_Is_Reported_At_The_Build(string suil)
    {
        var result = Run(suil);

        Assert.That(Ids(result), Has.Member("SUIL023").Or.Member("SUIL020"), string.Join("\n", result.GeneratorDiagnostics));
    }
}
