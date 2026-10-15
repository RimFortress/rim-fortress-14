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

namespace Content.Benchmarks._RF.CSS;

/// <summary>
/// The stylesheet side of <see cref="ISuilManager"/>: loading the files, compiling the rules and cascading them
/// onto a single control, with the real game stylesheets.
/// </summary>
[Virtual]
[MemoryDiagnoser]
public class SuilManagerBenchmark
{
    private TestPair _pair = default!;
    private ISuilManager _css = default!;
    private string[] _files = default!;

    private Dictionary<Kind, CssControl> _controls = default!;

    public enum Kind
    {
        Plain,
        Label,
        Button,
        CheckBox,
        LineEdit,
        Container,
    }

    public static IEnumerable<Kind> Kinds => Enum.GetValues<Kind>();

    [GlobalSetup]
    public async Task Setup()
    {
        _pair = await CssBenchmarkHost.GetPair();
        var client = _pair.Client;
        CssBenchmarkUtil.BindThread(client.InstanceDependencyCollection);

        _css = client.ResolveDependency<ISuilManager>();
        _files = CssBenchmarkUtil.ReadCorpus();

        if (_css.Stylesheet.Rules.Count == 0)
            throw new InvalidOperationException("The game stylesheet is empty: the benchmark would measure nothing.");

        _controls = new Dictionary<Kind, CssControl>
        {
            [Kind.Plain] = new CssControl(),
            [Kind.Label] = Styled(new CssControl { Text = "Some [bold]label[/bold] text" }, "label-heading"),
            [Kind.Button] = Styled(new CssButton { Text = "Button" }, "open-both", "danger"),
            [Kind.CheckBox] = new CssCheckBox(),
            [Kind.LineEdit] = new CssLineEdit { Text = "text" },
            [Kind.Container] = Styled(new CssBoxContainer(), "panel-dark-bordered"),
        };
    }

    private static T Styled<T>(T control, params string[] classes) where T : CssControl
    {
        foreach (var name in classes)
        {
            control.AddCssStyleClass(name);
        }

        return control;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _css.LoadDirectory(_css.StylesPath); // the benchmarks below replace the pending rules by the same ones
    }

    /// <summary>
    /// Reading and parsing every game CSS file (the part of <see cref="ISuilManager.Reload"/> before the compile).
    /// </summary>
    [Benchmark]
    public void LoadDirectory() => _css.LoadDirectory(_css.StylesPath);

    /// <summary>
    /// Compiling the parsed rules to typed properties, bucketing them by type and sorting by specificity.
    /// </summary>
    [Benchmark]
    public CssStylesheet BuildStylesheet() => _css.BuildStylesheet();

    /// <summary>
    /// Parsing and compiling all the files as strings, how the tests load the CSS.
    /// </summary>
    [Benchmark]
    public CssStylesheet LoadStringsAndBuild()
    {
        _css.ClearTestStylesheet();

        foreach (var file in _files)
        {
            _css.LoadSuilString(file);
        }

        return _css.BuildTestStylesheet();
    }

    /// <summary>
    /// The cascade of one control against the whole game stylesheet: finding the matching rules, sorting them,
    /// resolving the variables and <c>var()</c> declarations and applying the values.
    /// </summary>
    /// <remarks>
    /// The control is detached, so there are no ancestor selectors to walk and no descendants to restyle.
    /// </remarks>
    [Benchmark]
    [ArgumentsSource(nameof(Kinds))]
    public void ApplyStyle_OneControl(Kind kind) => _css.UpdateStyle(_controls[kind]);
}
