using System.Linq;
using BenchmarkDotNet.Attributes;
using Content.Client._RF.CSS;
using Robust.Shared.Analyzers;

namespace Content.Benchmarks._RF.CSS;

/// <summary>
/// The text to rules part of the CSS: the tokenizer and the parser, on the real game stylesheets. No engine needed.
/// </summary>
[Virtual]
[MemoryDiagnoser]
public class CssParserBenchmark
{
    private string[] _files = default!;
    private string _all = default!;
    private string _declarations = default!;
    private string _calc = default!;
    private string _selectors = default!;

    [GlobalSetup]
    public void Setup()
    {
        _files = CssBenchmarkUtil.ReadCorpus();
        _all = string.Join('\n', _files);

        _declarations = """
            margin: 4px 8px;
            padding: calc(2px + 1em) 4px;
            background: var(--panel-bg) no-repeat center / cover;
            border: 2px solid rgba(255, 255, 255, 0.5);
            font: bold 14px/1.2 "Fira", sans-serif;
            color: #aabbcc !important;
            transition: color 1s ease-in-out;
            """;

        _calc = "min-width: calc(100% - (2 * var(--gap) + 12px) / 3);";

        _selectors = string.Join('\n', Enumerable.Range(0, 200).Select(i =>
            $"#root-{i} > .a-{i}.b:hover > css-button .c:nth-child(2n+1):not(.d) {{ margin: 1px; }}"));
    }

    [Benchmark]
    public int Tokenize_AllFiles()
    {
        var count = 0;

        foreach (var file in _files)
        {
            count += CssTokenizer.Tokenize(file).Count;
        }

        return count;
    }

    [Benchmark]
    public int Tokenize_OneBigString() => CssTokenizer.Tokenize(_all).Count;

    [Benchmark]
    public int ParseStylesheet_AllFiles()
    {
        var count = 0;

        foreach (var file in _files)
        {
            count += CssParser.ParseStylesheet(file).Count;
        }

        return count;
    }

    [Benchmark]
    public int ParseDeclarations_Typical() => CssParser.ParseListOfDeclarations(_declarations).Count;

    [Benchmark]
    public int ParseDeclarations_Calc() => CssParser.ParseListOfDeclarations(_calc).Count;

    /// <summary>
    /// The selector heavy input: long compound selectors with combinators, pseudo-classes and An+B.
    /// </summary>
    [Benchmark]
    public int ParseStylesheet_ComplexSelectors() => CssParser.ParseStylesheet(_selectors).Count;
}
