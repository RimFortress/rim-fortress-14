using Content.Suil;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Content.Client._RF.Suil;
using Content.Client._RF.UserInterface.Controls;
using Content.IntegrationTests.Pair;
using Robust.Client.UserInterface;
using Robust.Shared.Analyzers;

namespace Content.Benchmarks._RF.Suil;

/// <summary>
/// The stylesheet side of <see cref="ISuilManager"/>: loading the files, compiling the rules and cascading them
/// onto a single control, with the real game stylesheets.
/// </summary>
[Virtual]
[MemoryDiagnoser]
public class SuilManagerBenchmark
{
    private TestPair _pair = default!;
    private ISuilManager _suil = default!;
    private string[] _files = default!;

    private Dictionary<Kind, UiElement> _controls = default!;

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
        _pair = await SuilBenchmarkHost.GetPair();
        var client = _pair.Client;
        SuilBenchmarkUtil.BindThread(client.InstanceDependencyCollection);

        _suil = client.ResolveDependency<ISuilManager>();
        _files = SuilBenchmarkUtil.ReadCorpus();

        if (_suil.Stylesheet.Rules.Count == 0)
            throw new InvalidOperationException("The game stylesheet is empty: the benchmark would measure nothing.");

        _controls = new Dictionary<Kind, UiElement>
        {
            [Kind.Plain] = new UiElement(),
            [Kind.Label] = Styled(new UiElement { Text = "Some [bold]label[/bold] text" }, "label-heading"),
            [Kind.Button] = Styled(new RfButton { Text = "Button" }, "open-both", "danger"),
            [Kind.CheckBox] = new RfCheckBox(),
            [Kind.LineEdit] = new RfLineEdit { Text = "text" },
            [Kind.Container] = Styled(new RfBoxContainer(), "panel-dark-bordered"),
        };
    }

    private static T Styled<T>(T control, params string[] classes) where T : UiElement
    {
        foreach (var name in classes)
        {
            control.AddSuilStyleClass(name);
        }

        return control;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _suil.LoadDirectory(_suil.StylesPath); // the benchmarks below replace the pending rules by the same ones
    }

    /// <summary>
    /// Reading and parsing every game SUIL file (the part of <see cref="ISuilManager.Reload"/> before the compile).
    /// </summary>
    [Benchmark]
    public void LoadDirectory() => _suil.LoadDirectory(_suil.StylesPath);

    /// <summary>
    /// Compiling the parsed rules to typed properties, bucketing them by type and sorting by specificity.
    /// </summary>
    [Benchmark]
    public SuilStylesheet BuildStylesheet() => _suil.BuildStylesheet();

    /// <summary>
    /// Parsing and compiling all the files as strings, how the tests load the SUIL.
    /// </summary>
    [Benchmark]
    public SuilStylesheet LoadStringsAndBuild()
    {
        _suil.ClearTestStylesheet();

        foreach (var file in _files)
        {
            _suil.LoadSuilString(file);
        }

        return _suil.BuildTestStylesheet();
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
    public void ApplyStyle_OneControl(Kind kind) => _suil.UpdateStyle(_controls[kind]);
}
