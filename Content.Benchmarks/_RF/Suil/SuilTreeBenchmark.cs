using Content.Suil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Content.Client._RF.Suil;
using Content.Client._RF.UserInterface;
using Content.Client._RF.UserInterface.Controls;
using Content.IntegrationTests.Pair;
using Robust.Client.UserInterface;
using Robust.Shared.Analyzers;
using Robust.Shared.IoC;
using Robust.Shared.Maths;

namespace Content.Benchmarks._RF.Suil;

/// <summary>
/// SUIL on a whole control tree, with the real game stylesheets: building it, restyling it in different ways and
/// laying it out. A row is 4 controls (a check box adds two more), so 100 rows are about 700 controls.
/// </summary>
[Virtual]
[MemoryDiagnoser]
public class SuilTreeBenchmark
{
    private TestPair _pair = default!;
    private ISuilManager _suil = default!;
    private IUserInterfaceManager _ui = default!;

    private RfBoxContainer _root = default!;
    private UiElement[] _leaves = default!;
    private bool _flip;
    private IDependencyCollection _deps = default!;

    // BenchmarkDotNet may run the benchmark on another thread than the setup, and IoC is thread local.
    private void Bind() => SuilBenchmarkUtil.BindThread(_deps);

    [Params(10, 100)]
    public int Rows { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        _pair = await SuilBenchmarkHost.GetPair();
        var client = _pair.Client;
        _deps = client.InstanceDependencyCollection;
        Bind();

        _suil = client.ResolveDependency<ISuilManager>();
        _ui = client.ResolveDependency<IUserInterfaceManager>();

        if (_suil.Stylesheet.Rules.Count == 0)
            throw new InvalidOperationException("The game stylesheet is empty: the benchmark would measure nothing.");

        _root = SuilBenchmarkUtil.BuildScreen(Rows);
        _root.SuilStyleIdentifier = "main-menu-fields"; // the id that child combinators of the game styles look at

        // In the root of the UI, so that the queue has the flusher to work with, like in the game.
        _ui.RootControl.AddChild(_root);
        _suil.FlushStyleUpdates();

        _leaves = Descendants(_root).OfType<UiElement>().Where(c => !c.Children.Any()).ToArray();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _root.Orphan();
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (var child in root.Children)
        {
            yield return child;

            foreach (var inner in Descendants(child))
            {
                yield return inner;
            }
        }
    }

    #region Building

    /// <summary>
    /// Nothing but the constructors: the injection, the text parsing, the child controls of a check box.
    /// </summary>
    [Benchmark]
    public int Build_Construct_Only()
    {
        Bind();
        var count = 0;

        for (var i = 0; i < Rows; i++)
        {
            var row = SuilBenchmarkUtil.BuildRow(i);
            count += row.ChildCount;
        }

        return count;
    }

    /// <summary>
    /// Rows are assembled first and attached to the root afterwards.
    /// Every attach restyles the whole subtree (<c>Parented</c>).
    /// </summary>
    [Benchmark]
    public RfBoxContainer Build_BottomUp()
    {
        Bind();
        return SuilBenchmarkUtil.BuildScreen(Rows);
    }

    /// <summary>
    /// Each control is attached before it is filled in, then the classes and the text change.
    /// </summary>
    [Benchmark]
    public RfBoxContainer Build_TopDown()
    {
        Bind();
        var root = SuilBenchmarkUtil.BuildScreenTopDown(Rows);
        _suil.FlushStyleUpdates();
        return root;
    }

    #endregion

    #region Restyling

    /// <summary>
    /// Restyling the whole tree, like on a stylesheet reload.
    /// </summary>
    [Benchmark]
    public void Restyle_WholeTree()
    {
        Bind();
        _suil.UpdateStyleRecursively(_root);
    }

    /// <summary>
    /// A variable on the root that every control inherits: the root and then everything below it is restyled.
    /// </summary>
    [Benchmark]
    public void Restyle_RootVariableChange()
    {
        Bind();
        _flip = !_flip;
        _root.SetSuilVariable("--benchmark-var", _flip ? "red" : "blue");
    }

    /// <summary>
    /// The id of the root is a part of the child combinators of the game styles, so the controls below it
    /// have to be matched again.
    /// </summary>
    [Benchmark]
    public void Restyle_AncestorIdentifierChange()
    {
        Bind();
        _flip = !_flip;
        _root.SuilStyleIdentifier = _flip ? "main-menu-fields" : "other";
        _suil.FlushStyleUpdates();
    }

    /// <summary>
    /// One class on one leaf, queued and flushed.
    /// </summary>
    [Benchmark]
    public void Restyle_OneClassOnOneLeaf()
    {
        Bind();
        _flip = !_flip;
        var leaf = _leaves[_leaves.Length / 2];

        if (_flip)
            leaf.AddSuilStyleClass("danger");
        else
            leaf.RemoveSuilStyleClass("danger");

        _suil.FlushStyleUpdates();
    }

    /// <summary>
    /// A class on every leaf at once: the flush has to sort and process all of them.
    /// </summary>
    [Benchmark]
    public void Restyle_ClassOnEveryLeaf()
    {
        Bind();
        _flip = !_flip;

        foreach (var leaf in _leaves)
        {
            if (_flip)
                leaf.AddSuilStyleClass("danger");
            else
                leaf.RemoveSuilStyleClass("danger");
        }

        _suil.FlushStyleUpdates();
    }

    /// <summary>
    /// The same control marked dirty many times before the frame: the flush should do it once.
    /// </summary>
    [Benchmark]
    public void Queue_SameControl_1000Times()
    {
        Bind();
        var leaf = _leaves[0];

        for (var i = 0; i < 1000; i++)
        {
            _suil.QueueStyleUpdate(leaf);
        }

        _suil.FlushStyleUpdates();
    }

    /// <summary>
    /// Hover on/off of a button, what the user does all the time.
    /// </summary>
    [Benchmark]
    public void Restyle_Hover()
    {
        Bind();
        var button = _leaves.OfType<RfButton>().First();
        button.SetSuilPseudoStyleClass(SuilPseudoClass.Hover, true);
        _suil.FlushStyleUpdates();
        button.SetSuilPseudoStyleClass(SuilPseudoClass.Hover, false);
        _suil.FlushStyleUpdates();
    }

    #endregion

    #region Layout

    /// <summary>
    /// Measuring and arranging the styled tree from scratch (the invalidation of the root is not enough to
    /// reach every child, so each control is invalidated).
    /// </summary>
    [Benchmark]
    public Vector2 Layout_Measure_Arrange_Cold()
    {
        Bind();
        InvalidateAll(_root);

        _root.Measure(new Vector2(1280, 4096));
        _root.Arrange(new UIBox2(0, 0, 1280, 4096));
        return _root.DesiredSize;
    }

    /// <summary>
    /// Nothing is invalid: the cost of the checks alone.
    /// </summary>
    [Benchmark]
    public Vector2 Layout_Measure_Arrange_Warm()
    {
        Bind();
        _root.Measure(new Vector2(1280, 4096));
        _root.Arrange(new UIBox2(0, 0, 1280, 4096));
        return _root.DesiredSize;
    }

    private static void InvalidateAll(Control control)
    {
        control.InvalidateMeasure();

        foreach (var child in control.Children)
        {
            InvalidateAll(child);
        }
    }

    #endregion
}
