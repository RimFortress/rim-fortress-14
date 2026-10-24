#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Content.Client._RF.Suil;
using Content.Client._RF.Suil.HotReload;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Suil;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._RF.Suil;

/// <summary>
/// The checks of the whole interface at once: every file reads and expands, and every view can be made, laid out and
/// reloaded from its file. A test of one element is not needed for it, a broken file or a tag that is not there is found
/// here.
/// </summary>
[TestFixture]
public sealed class SuilAllTest : GameTest
{
    [SidedDependency(Side.Client)] private readonly IResourceCache _resources = null!;
    [SidedDependency(Side.Client)] private readonly ISuilHotReload _reload = null!;
    [SidedDependency(Side.Client)] private readonly ISuilManager _suil = null!;
    [SidedDependency(Side.Client)] private readonly IUserInterfaceManager _ui = null!;

    // The views that can not be made on their own: they need the state of a game that has started.
    private static readonly HashSet<string> NotStandalone = new();

    [Test]
    [RunOnSide(Side.Client)]
    public void Every_Suil_File_Is_Read_And_Expanded()
    {
        var source = new ResourceSuilFileSource(_resources, new ResPath("/UI"));
        var globals = source.LoadGlobals();
        var files = _resources.ContentFindFiles(new ResPath("/UI"))
            .Where(path => path.Extension == "suil")
            .Select(source.ToSuilPath)
            .ToList();

        Assert.That(files, Is.Not.Empty);

        var errors = new List<string>();

        foreach (var file in files)
        {
            try
            {
                SuilExpander.Expand(source.Load(file), source, globals.Files.Contains(file) ? null : globals.Paths);
            }
            catch (Exception e)
            {
                errors.Add($"{file}: {e.Message}");
            }
        }

        Assert.That(errors, Is.Empty, string.Join("\n", errors));
        Assert.DoesNotThrow(() => _suil.Reload());
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Every_View_Is_Made_Laid_Out_And_Reloaded()
    {
        var views = typeof(UiElement).Assembly.GetTypes()
            .Where(type => typeof(ISuilView).IsAssignableFrom(type)
                           && typeof(Control).IsAssignableFrom(type)
                           && !type.IsAbstract
                           && type.GetConstructor(Type.EmptyTypes) != null
                           && !NotStandalone.Contains(type.FullName!))
            .OrderBy(type => type.FullName)
            .ToList();

        Assert.That(views, Has.Count.GreaterThan(30), "the views are found");

        var errors = new List<string>();

        foreach (var type in views)
        {
            Control? view = null;

            try
            {
                view = (Control) Activator.CreateInstance(type)!;
                _ui.WindowRoot.AddChild(view);
                _suil.UpdateStyleRecursively(view);
                view.Measure(new Vector2(1000, 800));
                view.Arrange(new UIBox2(0, 0, 1000, 800));

                var result = _reload.Reload((ISuilView) view);

                if (!result.Success)
                    errors.Add($"{type.FullName}: {string.Join("; ", result.Errors)}");
            }
            catch (Exception e)
            {
                errors.Add($"{type.FullName}: {e.GetType().Name} {e.Message}");
            }
            finally
            {
                view?.Orphan();
            }
        }

        Assert.That(errors, Is.Empty, string.Join("\n", errors));
    }
}
