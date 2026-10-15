using Content.Suil;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Content.Client._RF.CSS;
using Content.Client._RF.UserInterface.Controls;
using Content.IntegrationTests.Pair;
using Robust.Client.UserInterface;
using Robust.Shared.Analyzers;
using Robust.Shared.IoC;

namespace Content.Benchmarks._RF.CSS;

/// <summary>
/// What the lobby does when a list of many items is opened or changed: the items of the expedition equipment list
/// (the real <see cref="ExpeditionItemButton"/>, about 12 controls each) are made and put into a container that is
/// already on the screen, and the old ones are removed.
/// </summary>
[Virtual]
[MemoryDiagnoser]
public class CssListBenchmark
{
    private TestPair _pair = default!;
    private ISuilManager _css = default!;
    private IUserInterfaceManager _ui = default!;
    private IDependencyCollection _deps = default!;

    private CssBoxContainer _box = default!;
    private CssControl[] _leaves = default!;

    [Params(25, 100, 300)]
    public int Items { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        _pair = await CssBenchmarkHost.GetPair();
        var client = _pair.Client;
        _deps = client.InstanceDependencyCollection;
        Bind();

        _css = client.ResolveDependency<ISuilManager>();
        _ui = client.ResolveDependency<IUserInterfaceManager>();

        _box = new CssBoxContainer { FlexDirection = CssFlexDirection.Column };
        _ui.RootControl.AddChild(_box);

        _leaves = new CssControl[Items];

        for (var i = 0; i < _leaves.Length; i++)
        {
            _leaves[i] = new CssControl { Text = "leaf " + i };
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _box.Orphan();
    }

    // BenchmarkDotNet may run the benchmark on another thread than the setup, and IoC is thread local.
    private void Bind() => CssBenchmarkUtil.BindThread(_deps);

    /// <summary>
    /// The layout of <see cref="ExpeditionItemButton"/> (see its XAML), without the entity view that needs the
    /// sprite system of a running game: 10 controls, an <see cref="IntCounter"/> with two buttons and a line edit.
    /// </summary>
    private static CssBoxContainer MakeItem(int i)
    {
        var item = new CssBoxContainer { FlexDirection = CssFlexDirection.Row };
        item.AddCssStyleClass("panel-dark-bordered");

        var view = new CssBoxContainer { FlexDirection = CssFlexDirection.Row };
        view.AddCssStyleClass("panel-light-bordered");
        view.AddChild(new CssControl { MinWidth = 36, MinHeight = 36 });
        item.AddChild(view);

        item.AddChild(Styled(new CssControl(), "expedition-item-divider", "low-divider"));
        item.AddChild(Styled(new CssControl { Text = "Item name " + i }, "expedition-item-name", "placeholder-text"));
        item.AddChild(Styled(new CssControl { Text = "Cost: " + (10 + i) }, "expedition-item-cost", "placeholder-text"));
        item.AddChild(Styled(new CssControl(), "expedition-item-divider", "low-divider"));
        item.AddChild(new IntCounter { MinValue = 0 });
        return item;
    }

    private static CssControl Styled(CssControl control, string id, string @class)
    {
        control.CssStyleIdentifier = id;
        control.AddCssStyleClass(@class);
        return control;
    }

    private void Fill(Control box)
    {
        for (var i = 0; i < Items; i++)
        {
            box.AddChild(MakeItem(i));
        }
    }

    /// <summary>
    /// Only making the items: XAML, injection, the child controls.
    /// </summary>
    [Benchmark]
    public int Items_Construct_Only()
    {
        Bind();
        var count = 0;

        for (var i = 0; i < Items; i++)
        {
            count += MakeItem(i).ChildCount;
        }

        return count;
    }

    /// <summary>
    /// What <c>BuildItems</c> does: remove the old items, make new ones, add them to the box on the screen, and
    /// let the frame restyle everything queued.
    /// </summary>
    [Benchmark]
    public void Items_Rebuild_OnScreen()
    {
        Bind();
        _box.RemoveAllChildren();
        Fill(_box);
        _css.FlushStyleUpdates();
    }

    /// <summary>
    /// The same, but into a box that is not on the screen yet and is attached when it is full.
    /// </summary>
    [Benchmark]
    public void Items_Rebuild_FillThenAttach()
    {
        Bind();
        _box.RemoveAllChildren();
        _box.Orphan();

        var box = new CssBoxContainer { FlexDirection = CssFlexDirection.Column };
        Fill(box);
        _ui.RootControl.AddChild(box);
        _css.FlushStyleUpdates();
        box.Orphan();
        _ui.RootControl.AddChild(_box);
    }

    /// <summary>
    /// Removing a full list: every removal looks at the siblings that are left.
    /// </summary>
    [Benchmark]
    public void Items_Remove_All()
    {
        Bind();
        Fill(_box);
        _css.FlushStyleUpdates();
        _box.RemoveAllChildren();
        _css.FlushStyleUpdates();
    }

    /// <summary>
    /// Adding already made children of the simplest kind to a box on the screen: what is left once the cost of
    /// the items themselves is taken away (every addition looks at the siblings).
    /// </summary>
    [Benchmark]
    public void Leaves_Add_OnScreen()
    {
        Bind();

        foreach (var leaf in _leaves)
        {
            _box.AddChild(leaf);
        }

        _css.FlushStyleUpdates();

        foreach (var leaf in _leaves)
        {
            leaf.Orphan();
        }
    }
}
