#nullable enable
using System.Collections.Generic;
using System.Linq;
using Content.Client._RF.Construction.Controls;
using Content.Client._RF.Suil;
using Content.Client._RF.Suil.HotReload;
using Content.Client._RF.UserInterface.Controls;
using Content.Client._RF.World.UI;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Suil;
using Robust.Client.UserInterface;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._RF.Suil;

/// <summary>
/// The windows of the world map (<c>elements/world/world.suil</c>) and of the construction menu
/// (<c>elements/construction/construction.suil</c>).
/// </summary>
[TestFixture]
public sealed class SuilWorldConstructionTest : GameTest
{
    [SidedDependency(Side.Client)] private readonly ISuilHotReload _reload = null!;
    [SidedDependency(Side.Client)] private readonly IPrototypeManager _proto = null!;
    [SidedDependency(Side.Client)] private readonly ISuilManager _suil = null!;
    [SidedDependency(Side.Client)] private readonly IUserInterfaceManager _ui = null!;

    [Test]
    [RunOnSide(Side.Client)]
    public void ContextWindow_Shows_The_Buttons_Of_Its_State_And_Has_No_Header()
    {
        var window = new WorldMapContextWindow();

        Assert.That(window.HeaderVisible, Is.False);

        window.SetState(WorldMapContextWindowState.World);
        Assert.That(
            (window.AddMarkerButton.Visible, window.ChangeMarkerButton.Visible, window.DeleteMarkerButton.Visible, window.TpHereButton.Visible),
            Is.EqualTo((true, false, false, false)));

        window.SetState(WorldMapContextWindowState.Marker);
        Assert.That(
            (window.AddMarkerButton.Visible, window.ChangeMarkerButton.Visible, window.DeleteMarkerButton.Visible, window.TpHereButton.Visible),
            Is.EqualTo((false, true, true, true)));

        window.SetState(WorldMapContextWindowState.NoChangeMarker);
        Assert.That(
            (window.AddMarkerButton.Visible, window.ChangeMarkerButton.Visible, window.DeleteMarkerButton.Visible, window.TpHereButton.Visible),
            Is.EqualTo((false, false, false, true)));
        Assert.That(window.DeleteMarkerButton.HasSuilStyleClass("red"), Is.True);

        var result = _reload.Reload(window);
        Assert.That(result.Success, Is.True, string.Join("\n", result.Errors));
        Assert.That(window.HeaderVisible, Is.False, "a property of the file stays through a reload");
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void ContextWindow_Is_As_High_As_Its_Buttons()
    {
        var window = new WorldMapContextWindow();

        Assert.That(window.MinSize, Is.EqualTo(System.Numerics.Vector2.Zero), "the default of a window is 200x150");

        window.Measure(new System.Numerics.Vector2(float.PositiveInfinity));
        Assert.That(window.DesiredSize.Y, Is.LessThan(100f), "one button of the world is not a window of 150");
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void ConstructionButton_Divider_Is_As_High_As_The_Button()
    {
        var proto = _proto.EnumeratePrototypes<EntityPrototype>().First(p => !p.Abstract);
        var button = new ConstructionButton("construction-test", proto);

        button.Measure(new System.Numerics.Vector2(400, 100));
        button.Arrange(new UIBox2(0, 0, 400, 100));

        var row = button.Children.Single();
        var divider = row.Children.ElementAt(1);

        Assert.That(divider.Height, Is.GreaterThan(40f), "a strip, not a couple of pixels");
        Assert.That(divider.Height, Is.GreaterThan(row.Height * 0.8f).Or.GreaterThan(button.Height * 0.5f));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void ColorSelector_Name_Of_The_Color_Follows_The_Button_And_The_Window_Has_No_Fixed_Width()
    {
        var sliders = new RfColorSelectorSliders();
        var view = (ISuilView) sliders;
        var type = (Control) view.SuilGetField("type-selector")!;
        var description = (Control) view.SuilGetField("description-label")!;

        sliders.Measure(new System.Numerics.Vector2(500, 300));
        sliders.Arrange(new UIBox2(0, 0, 500, 300));

        Assert.That(description.Position.X, Is.EqualTo(type.Position.X + type.Width + 6f).Within(1f), "next to the button, not in the middle of the space");

        var window = new ColorPickerWindow();
        Assert.That(window.SetWidth, Is.AnyOf(0f, float.NaN), "the width of the window is the width of its contents");
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void ColorSelector_Does_Not_Change_Its_Width_With_The_Name_Of_The_Color()
    {
        var sliders = new RfColorSelectorSliders();
        var view = (ISuilView) sliders;
        var description = (UiElement) view.SuilGetField("description-label")!;
        var slider = (UiElement) view.SuilGetField("top-slider")!;

        _ui.WindowRoot.AddChild(sliders);

        try
        {
            _suil.UpdateStyleRecursively(sliders);

            Assert.That(description.MaxWidth, Is.EqualTo(150f), "the name is cut, it doesn't push the window");
            Assert.That(slider.MinWidth, Is.GreaterThanOrEqualTo(100f), "a slider is never tiny");
        }
        finally
        {
            sliders.Orphan();
        }
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void MarkerWindow_Starts_With_The_Color_Of_The_Sliders_And_Reloads()
    {
        var window = new WorldMarkerWindow();
        var view = (ISuilView) window;
        var panel = (UiElement) view.SuilGetField("color-panel")!;
        var sliders = (RfColorSelectorSliders) view.SuilGetField("color-selectors")!;

        Assert.That(panel.BackgroundColor, Is.EqualTo(sliders.Color));

        var result = _reload.Reload(window);
        Assert.That(result.Success, Is.True, string.Join("\n", result.Errors));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void ConstructionMenu_Is_Built_With_Its_Lists_And_Reloads()
    {
        var menu = new RfConstructionMenu();

        Assert.That(menu.Metadata.Visible, Is.False);
        Assert.That(menu.ItemList.Visible, Is.False);
        Assert.That(menu.CategoryList.Visible, Is.True);
        Assert.That(menu.EraseButton.ToggleMode, Is.True);
        Assert.That(menu.EraseButton.HasSuilStyleClass("open-both"), Is.True);
        Assert.That(((ISuilView) menu).SuilViewName, Is.EqualTo("RfConstructionMenu"));

        menu.EnsureSetup();

        var result = _reload.Reload(menu);
        Assert.That(result.Success, Is.True, string.Join("\n", result.Errors));
        Assert.That(menu.EraseButton, Is.Not.Null);
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void WorldMapWindow_Gives_The_Map_The_Space_Under_The_Header_And_Reloads()
    {
        var window = new WorldMapWindow();
        var view = (ISuilView) window;
        var area = (UiElement) view.SuilGetField("map-area")!;

        Assert.That(window.WorldMap, Is.Not.Null);
        Assert.That(window.WorldMap.Parent, Is.SameAs(area));

        area.Measure(new System.Numerics.Vector2(400, 300));
        area.Arrange(new UIBox2(0, 0, 400, 300));
        Assert.That(window.WorldMap.SetSize, Is.EqualTo(new System.Numerics.Vector2(400, 300)));

        var result = _reload.Reload(window);
        Assert.That(result.Success, Is.True, string.Join("\n", result.Errors));
        Assert.That(window.WorldMap.Parent, Is.SameAs(area), "the map is kept through a reload");
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void ConstructionMenu_Panels_And_Search_Take_The_Whole_Width()
    {
        var menu = new RfConstructionMenu();
        var view = (ISuilView) menu;
        var searchBar = (UiElement) view.SuilGetField("search-bar")!;
        var search = (Control) view.SuilGetField("search")!;

        searchBar.Visible = true;
        menu.Metadata.Visible = true;

        menu.Measure(new System.Numerics.Vector2(500, 700));
        menu.Arrange(new UIBox2(0, 0, 500, 700));

        Assert.That(searchBar.Width, Is.EqualTo(menu.Metadata.Width).Within(0.5f), "the panels have one width");
        Assert.That(search.Width, Is.GreaterThan(searchBar.Width * 0.7f), "the search takes what the button leaves");
        Assert.That(menu.Metadata.Width, Is.GreaterThan(400f));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void ConstructionButton_Centers_Its_Text_In_The_Height_Of_The_Button()
    {
        var proto = _proto.EnumeratePrototypes<EntityPrototype>().First(p => !p.Abstract);
        var button = new ConstructionButton("construction-test", proto);
        var label = (UiElement) ((ISuilView) button).SuilGetField("name-label")!;

        button.Measure(new System.Numerics.Vector2(400, 100));
        button.Arrange(new UIBox2(0, 0, 400, 100));

        Assert.That(button.Width, Is.EqualTo(396f).Within(0.5f), "the button takes the width of the list less its margins");
        Assert.That(label.Position.Y + label.Height / 2f, Is.EqualTo(button.Height / 2f).Within(3f));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void WorldMap_Merges_The_Tiles_Into_Regions_Of_The_Right_Size()
    {
        var map = new WorldMapControl();
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var tiles = (Dictionary<Vector2i, Color>) typeof(WorldMapControl).GetField("_tiles", flags)!.GetValue(map)!;
        var regions = (Dictionary<Color, HashSet<Box2>>) typeof(WorldMapControl).GetField("_regions", flags)!.GetValue(map)!;

        // a block of 3x2 and one more tile of the same color, and a tile of another one
        for (var x = 0; x < 3; x++)
        {
            for (var y = 0; y < 2; y++)
            {
                tiles[new Vector2i(x, y)] = Color.Red;
            }
        }

        tiles[new Vector2i(5, 5)] = Color.Red;
        tiles[new Vector2i(1, -3)] = Color.Blue;

        typeof(WorldMapControl).GetMethod("MergeTileRegions", flags)!.Invoke(map, null);

        var area = regions.Values.SelectMany(set => set).Sum(box => box.Width * box.Height);
        Assert.That(area, Is.EqualTo(tiles.Count), "every tile is in a region once");
        Assert.That(regions[Color.Red], Does.Contain(new Box2(0, 0, 3, 2)));
        Assert.That(regions[Color.Blue], Does.Contain(new Box2(1, -3, 2, -2)));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void ConstructionStep_Is_Shifted_By_Its_Depth()
    {
        var step = new ConstructionStep("Weld the frame", null, 2);
        var view = (ISuilView) step;
        var label = (UiElement) view.SuilGetField("text-label")!;

        Assert.That(step.Margin.Left, Is.EqualTo(16f));
        Assert.That(label.Text, Is.EqualTo("Weld the frame"));
        Assert.That(_reload.Reload(step).Success, Is.True);
    }
}
