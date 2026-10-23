#nullable enable
using System.Linq;
using System.Numerics;
using Content.Client._RF.Info.Controls;
using Content.Client._RF.Suil;
using Content.Client._RF.Suil.HotReload;
using Content.Client._RF.UserInterface.Controls;
using Content.Shared._RF.NPC.Components;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Suil;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._RF.Suil;

/// <summary>
/// The information about a pop (<c>elements/info/info.suil</c>): the windows are built by the generated code, the tabs
/// refresh themselves with the scope, and the new bar of the progress.
/// </summary>
[TestFixture]
public sealed class SuilInfoControlsTest : GameTest
{
    [SidedDependency(Side.Client)] private readonly ISuilHotReload _reload = null!;

    private sealed class CountingTab(float refreshSeconds) : InfoTab(refreshSeconds)
    {
        public int Updates;

        protected override void Update(EntityUid uid) => Updates++;
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void PopInfoWindow_Has_All_Its_Tabs_And_Reloads()
    {
        var window = new PopInfoWindow();
        var view = (ISuilView) window;
        var tab = (RfTabContainer) view.SuilGetField("tab")!;

        Assert.That(tab.Tabs, Has.Count.EqualTo(6));
        Assert.That(tab.Tabs.Select(t => t.Page), Is.EqualTo(new[]
        {
            view.SuilGetField("skills"),
            view.SuilGetField("jobs"),
            view.SuilGetField("health"),
            view.SuilGetField("mood"),
            view.SuilGetField("opinion"),
            view.SuilGetField("inventory"),
        }));
        Assert.That(tab.Tabs.All(t => !string.IsNullOrEmpty(t.Title)), Is.True, "the titles are set by the code");
        Assert.That(tab.Tabs.All(t => t.Page is IInfoTab), Is.True);
        Assert.That(view.SuilGetField("sprite"), Is.Not.Null);

        var result = _reload.Reload(window);
        Assert.That(result.Success, Is.True, string.Join("\n", result.Errors));
        Assert.That(tab.Tabs, Has.Count.EqualTo(6), "the tabs stay through a reload");
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void The_Tabs_And_The_Rows_Are_Built_And_Reload()
    {
        Control[] views =
        [
            new SkillsInfoTab(),
            new JobsInfoTab(),
            new HealthInfoTab(),
            new MoodInfoTab(),
            new OpinionInfoTab(),
            new OpinionInfoButton(),
            new SocializationEffectInfo(),
            new SocializationEffectsContainer(),
        ];

        foreach (var view in views)
        {
            var result = _reload.Reload((ISuilView) view);
            Assert.That(result.Success, Is.True, $"{view.GetType().Name}: {string.Join("\n", result.Errors)}");
        }
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void JobInfo_Puts_The_Icon_The_Name_And_The_Priority_On_One_Line()
    {
        var job = new JobInfo(new EntityUid(1), new NpcJob());
        var view = (ISuilView) job;
        var icon = (Control) view.SuilGetField("icon")!;
        var label = (Control) view.SuilGetField("label")!;
        var priority = (Control) view.SuilGetField("priority")!;

        job.Measure(new Vector2(200, 100));
        job.Arrange(new UIBox2(0, 0, 200, 100));

        float Middle(Control control) => control.Position.Y + control.Height / 2f;

        Assert.That(Middle(icon), Is.EqualTo(Middle(priority)).Within(0.6f));
        Assert.That(Middle(label), Is.EqualTo(Middle(priority)).Within(0.6f));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void The_Effects_Go_In_Stripes_By_The_Class_Of_The_List()
    {
        var list = new SocializationEffectsContainer();
        var effect = new SocializationEffectInfo();

        Assert.That(list.HasSuilStyleClass("effects-list"), Is.True);
        Assert.That(effect.HasSuilStyleClass("effect-info"), Is.True);
        Assert.That(effect.MouseFilter, Is.EqualTo(Control.MouseFilterMode.Stop), "the tooltip needs the mouse");
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void InfoTab_Refreshes_Itself_While_It_Is_In_The_Tree()
    {
        var tab = new CountingTab(1f);
        var uid = new EntityUid(1);

        tab.Scope.Enter();
        tab.Scope.Update(2f, true);
        Assert.That(tab.Updates, Is.Zero, "nothing to show before the window gives a pop");

        tab.UpdateInfo(uid);
        Assert.That(tab.Updates, Is.EqualTo(1));

        tab.Scope.Update(0.5f, true);
        Assert.That(tab.Updates, Is.EqualTo(1));

        tab.Scope.Update(0.6f, true);
        Assert.That(tab.Updates, Is.EqualTo(2));

        tab.Scope.Update(5f, false);
        Assert.That(tab.Updates, Is.EqualTo(2), "a hidden tab is not refreshed");

        tab.Scope.Exit();
        tab.Scope.Update(5f, true);
        Assert.That(tab.Updates, Is.EqualTo(2), "nor is the one that left the tree");

        var manual = new CountingTab(0f);
        manual.Scope.Enter();
        manual.UpdateInfo(uid);
        manual.Scope.Update(60f, true);
        Assert.That(manual.Updates, Is.EqualTo(1), "0 is a tab that is refreshed by its events");
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void ProgressBar_Fills_A_Part_Of_The_Track_And_Puts_The_Children_Over_It()
    {
        var bar = new RfProgressBar { MinValue = 100, MaxValue = 300 };
        var text = new UiElement();
        bar.AddChild(text);

        Assert.That(bar.HasSuilStyleClass("progress"), Is.True);
        Assert.That(bar.Fill.HasSuilStyleClass("fill"), Is.True);

        bar.Value = 150;
        Assert.That(bar.Ratio, Is.EqualTo(0.25f));

        bar.Measure(new Vector2(200, 30));
        bar.Arrange(new UIBox2(0, 0, 200, 30));

        Assert.That(bar.Fill.Width, Is.EqualTo(50f).Within(0.01f));
        Assert.That(bar.Fill.Height, Is.EqualTo(30f).Within(0.01f));
        Assert.That(text.Width, Is.EqualTo(200f).Within(0.01f), "a child gets the whole track");

        bar.Value = 1000;
        Assert.That(bar.Value, Is.EqualTo(300f), "the value stays between the limits");
        Assert.That(bar.Ratio, Is.EqualTo(1f));

        bar.MaxValue = 200;
        Assert.That(bar.Value, Is.EqualTo(200f), "a new limit takes the value with it");
    }
}
