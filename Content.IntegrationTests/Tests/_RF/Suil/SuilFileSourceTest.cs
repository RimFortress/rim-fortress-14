#nullable enable
using System.IO;
using System.Linq;
using Content.Client._RF.Suil.HotReload;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Suil;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._RF.Suil;

/// <summary>
/// How the hot reload finds out that a file was edited: the sandbox has no file watcher, so what was read is remembered
/// and read again when the window gets the focus.
/// </summary>
[TestFixture]
public sealed class SuilFileSourceTest : GameTest
{
    [SidedDependency(Side.Client)] private readonly IResourceCache _resources = null!;
    [SidedDependency(Side.Client)] private readonly ISuilHotReload _reload = null!;
    [SidedDependency(Side.Client)] private readonly IUserInterfaceManager _ui = null!;

    private MemoryContentRoot Mount(string folder, params (string Path, string Text)[] files)
    {
        var root = new MemoryContentRoot();

        foreach (var (path, text) in files)
        {
            root.AddOrUpdateFile(new ResPath(path), text);
        }

        _resources.AddRoot(new ResPath(folder), root);
        return root;
    }

    private ResourceSuilFileSource Source(string folder) => new(_resources, new ResPath(folder));

    [Test]
    [RunOnSide(Side.Client)]
    public void Files_Are_Read_By_Their_Path_In_The_Language()
    {
        Mount("/TestSuilA", ("/dir/a.suil", "box { }"), ("/b.suil", "box { rf-button }"));
        var source = Source("/TestSuilA");

        Assert.That(source.Has("/dir/a.suil"), Is.True);
        Assert.That(source.Has("/nope.suil"), Is.False);
        Assert.That(source.Load("/b.suil").Root!.Children, Has.Count.EqualTo(1));
        Assert.That(source.List("/").OrderBy(p => p), Is.EqualTo(new[] { "/b.suil", "/dir/a.suil" }));
        Assert.That(source.ToSuilPath(new ResPath("/TestSuilA/dir/a.suil")), Is.EqualTo("/dir/a.suil"));
        Assert.Throws<FileNotFoundException>(() => source.Load("/nope.suil"));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void A_File_That_Was_Edited_Is_Found()
    {
        var root = Mount("/TestSuilB", ("/a.suil", "box { }"), ("/b.suil", "box { }"));
        var source = Source("/TestSuilB");

        source.Forget();
        source.Load("/a.suil");
        source.Load("/b.suil");
        source.MarkScanned();

        Assert.That(source.FindChanges().Any, Is.False, "nothing was edited");

        root.AddOrUpdateFile(new ResPath("/a.suil"), "box { rf-button }");
        var changes = source.FindChanges();

        Assert.That(changes.Changed, Is.EqualTo(new[] { "/a.suil" }));
        Assert.That(changes.Added, Is.Empty);
        Assert.That(changes.Removed, Is.Empty);
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void A_File_That_Was_Added_Or_Removed_Is_Found()
    {
        var root = Mount("/TestSuilC", ("/a.suil", "box { }"), ("/b.suil", "box { }"));
        var source = Source("/TestSuilC");

        source.Forget();
        source.Load("/a.suil");
        source.Load("/b.suil");
        source.MarkScanned();

        root.AddOrUpdateFile(new ResPath("/c.suil"), "box { }");
        root.RemoveFile(new ResPath("b.suil")); // the root keeps relative paths
        var changes = source.FindChanges();

        Assert.That(changes.Added, Is.EqualTo(new[] { "/c.suil" }));
        Assert.That(changes.Removed, Is.EqualTo(new[] { "/b.suil" }));
        Assert.That(changes.Changed, Is.Empty);
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Reading_The_Files_Again_Makes_Them_The_Baseline()
    {
        var root = Mount("/TestSuilD", ("/a.suil", "box { }"));
        var source = Source("/TestSuilD");

        source.Forget();
        source.Load("/a.suil");
        source.MarkScanned();

        root.AddOrUpdateFile(new ResPath("/a.suil"), "box { rf-button }");
        Assert.That(source.FindChanges().Any, Is.True);

        source.Load("/a.suil");
        Assert.That(source.FindChanges().Any, Is.False, "what was read last is what later edits are compared with");
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void The_Globals_Are_The_List_And_The_Files_It_Brings()
    {
        Mount("/TestSuilE",
            ("/global-usings.suil", "use \"/theme\";"),
            ("/theme.suil", "use \"/base\";\nlet gold = #B07D2B;"),
            ("/base.suil", "let size = 12;"),
            ("/view.suil", "box { }"));

        var globals = Source("/TestSuilE").LoadGlobals();

        Assert.That(globals.Paths, Is.EqualTo(new[] { "/theme.suil" }));
        Assert.That(globals.Files, Is.EquivalentTo(new[] { "/theme.suil", "/base.suil" }), "these don't get the globals themselves");
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Without_Edits_The_Focus_Check_Does_Nothing()
    {
        // the real files of the game were read when the manager started, and nobody has edited them since
        Assert.That(_reload.ReloadChanged().Any, Is.False);
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void A_Reload_By_Path_Reloads_The_Views_That_Are_In_The_Ui()
    {
        var view = new GeneratedHotReloadView();
        _ui.WindowRoot.AddChild(view);

        try
        {
            // the leading slash and the extension are optional, like in [SuilView]
            var results = _reload.ReloadPath("_RF/tests/hot-reload-view.suil").Where(r => ReferenceEquals(r.View, view)).ToList();

            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(results[0].Success, Is.True, string.Join("\n", results[0].Errors));
            Assert.That(_reload.ReloadPath("/no/such/view"), Is.Empty);
        }
        finally
        {
            view.Orphan();
        }
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Picking_Waits_For_One_Click_And_Can_Be_Stopped()
    {
        Assert.That(_reload.IsPicking, Is.False);

        _reload.StartPicking();
        _reload.StartPicking();
        Assert.That(_reload.IsPicking, Is.True);

        _reload.StopPicking();
        Assert.That(_reload.IsPicking, Is.False);

        _reload.StopPicking(); // nothing to stop is not an error
    }
}
