using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Client._RF.Suil;
using Content.Client._RF.UserInterface.Controls;
using Robust.Shared.IoC;

namespace Content.Benchmarks._RF.Suil;

/// <summary>
/// Shared setup of the SUIL benchmarks.
/// </summary>
internal static class SuilBenchmarkUtil
{
    /// <summary>
    /// Text of every SUIL file of the game (<c>Resources/UI</c>), the same corpus the client loads on start.
    /// </summary>
    public static string[] ReadCorpus()
    {
        var dir = AppContext.BaseDirectory;

        while (dir != null)
        {
            var suil = Path.Combine(dir, "Resources", "UI");

            if (Directory.Exists(suil))
            {
                // test.suil and unsupported.suil are not loaded as the game styles (see the Sawmill "suil" in the client).
                return Directory.EnumerateFiles(suil, "*.suil", SearchOption.AllDirectories)
                    .Where(f => !Path.GetFileName(f).StartsWith('.'))
                    .OrderBy(f => f, StringComparer.Ordinal)
                    .Select(File.ReadAllText)
                    .Where(text => !text.Contains("use \"")) // the lists of imports need a source of files
                    .ToArray();
            }

            dir = Path.GetDirectoryName(dir);
        }

        throw new DirectoryNotFoundException("Resources/UI not found above " + AppContext.BaseDirectory);
    }

    /// <summary>
    /// The SUIL controls and the managers use <see cref="IoCManager"/>, which is thread local: the benchmark thread has
    /// to use the dependencies of the client of the pair.
    /// </summary>
    public static void BindThread(IDependencyCollection client)
    {
        IoCManager.InitThread(client, replaceExisting: true);
    }

    /// <summary>
    /// Counts the controls of the tree, the root included.
    /// </summary>
    public static int Count(Robust.Client.UserInterface.Control root)
    {
        var count = 1;

        foreach (var child in root.Children)
        {
            count += Count(child);
        }

        return count;
    }

    private static readonly string[] ButtonClasses =
    [
        "open-left",
        "open-both",
        "open-right",
        "danger",
        "good",
    ];

    /// <summary>
    /// Builds a row like the ones in the lobby: a label, a button, a check box and a line edit in a flex container.
    /// </summary>
    public static RfBoxContainer BuildRow(int index)
    {
        var row = new RfBoxContainer();
        row.AddSuilStyleClass("panel-dark");

        var label = new UiElement { Text = $"[bold]Row[/bold] {index}" };
        label.AddSuilStyleClass("label-heading");

        var button = new RfButton { Text = "Button " + index };
        button.AddSuilStyleClass(ButtonClasses[index % ButtonClasses.Length]);

        var check = new RfCheckBox();
        check.Label.Text = "Check " + index;

        var edit = new RfLineEdit { Text = "text " + index };

        row.AddChild(label);
        row.AddChild(button);
        row.AddChild(check);
        row.AddChild(edit);
        return row;
    }

    /// <summary>
    /// Builds a screen of <paramref name="rows"/> rows, attaching every finished row to the root.
    /// </summary>
    public static RfBoxContainer BuildScreen(int rows)
    {
        var root = new RfBoxContainer { FlexDirection = SuilFlexDirection.Column };
        root.AddSuilStyleClass("panel-dark");

        for (var i = 0; i < rows; i++)
        {
            root.AddChild(BuildRow(i));
        }

        return root;
    }

    /// <summary>
    /// Builds the same screen the other way around: the root first, so that each control is attached to a
    /// parent that is attached already, and then filled.
    /// </summary>
    public static RfBoxContainer BuildScreenTopDown(int rows)
    {
        var root = new RfBoxContainer { FlexDirection = SuilFlexDirection.Column };
        root.AddSuilStyleClass("panel-dark");

        for (var i = 0; i < rows; i++)
        {
            var row = new RfBoxContainer();
            root.AddChild(row);
            row.AddSuilStyleClass("panel-dark");

            var label = new UiElement();
            row.AddChild(label);
            label.AddSuilStyleClass("label-heading");
            label.Text = $"[bold]Row[/bold] {i}";

            var button = new RfButton();
            row.AddChild(button);
            button.AddSuilStyleClass(ButtonClasses[i % ButtonClasses.Length]);
            button.Text = "Button " + i;

            var check = new RfCheckBox();
            row.AddChild(check);
            check.Label.Text = "Check " + i;

            var edit = new RfLineEdit();
            row.AddChild(edit);
            edit.Text = "text " + i;
        }

        return root;
    }
}
