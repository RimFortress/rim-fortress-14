using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Client._RF.CSS;
using Content.Client._RF.UserInterface.Controls;
using Robust.Shared.IoC;

namespace Content.Benchmarks._RF.CSS;

/// <summary>
/// Shared setup of the CSS benchmarks.
/// </summary>
internal static class CssBenchmarkUtil
{
    /// <summary>
    /// Text of every CSS file of the game (<c>Resources/CSS</c>), the same corpus the client loads on start.
    /// </summary>
    public static string[] ReadCorpus()
    {
        var dir = AppContext.BaseDirectory;

        while (dir != null)
        {
            var css = Path.Combine(dir, "Resources", "CSS");

            if (Directory.Exists(css))
            {
                // test.css and unsupported.css are not loaded as the game styles (see the Sawmill "css" in the client).
                return Directory.EnumerateFiles(css, "*.css", SearchOption.AllDirectories)
                    .Where(f => !Path.GetFileName(f).StartsWith('.'))
                    .OrderBy(f => f, StringComparer.Ordinal)
                    .Select(File.ReadAllText)
                    .ToArray();
            }

            dir = Path.GetDirectoryName(dir);
        }

        throw new DirectoryNotFoundException("Resources/CSS not found above " + AppContext.BaseDirectory);
    }

    /// <summary>
    /// The CSS controls and the managers use <see cref="IoCManager"/>, which is thread local: the benchmark thread has
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
    public static CssBoxContainer BuildRow(int index)
    {
        var row = new CssBoxContainer();
        row.AddCssStyleClass("panel-dark");

        var label = new CssControl { Text = $"[bold]Row[/bold] {index}" };
        label.AddCssStyleClass("label-heading");

        var button = new CssButton { Text = "Button " + index };
        button.AddCssStyleClass(ButtonClasses[index % ButtonClasses.Length]);

        var check = new CssCheckBox();
        check.Label.Text = "Check " + index;

        var edit = new CssLineEdit { Text = "text " + index };

        row.AddChild(label);
        row.AddChild(button);
        row.AddChild(check);
        row.AddChild(edit);
        return row;
    }

    /// <summary>
    /// Builds a screen of <paramref name="rows"/> rows, attaching every finished row to the root.
    /// </summary>
    public static CssBoxContainer BuildScreen(int rows)
    {
        var root = new CssBoxContainer { FlexDirection = CssFlexDirection.Column };
        root.AddCssStyleClass("panel-dark");

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
    public static CssBoxContainer BuildScreenTopDown(int rows)
    {
        var root = new CssBoxContainer { FlexDirection = CssFlexDirection.Column };
        root.AddCssStyleClass("panel-dark");

        for (var i = 0; i < rows; i++)
        {
            var row = new CssBoxContainer();
            root.AddChild(row);
            row.AddCssStyleClass("panel-dark");

            var label = new CssControl();
            row.AddChild(label);
            label.AddCssStyleClass("label-heading");
            label.Text = $"[bold]Row[/bold] {i}";

            var button = new CssButton();
            row.AddChild(button);
            button.AddCssStyleClass(ButtonClasses[i % ButtonClasses.Length]);
            button.Text = "Button " + i;

            var check = new CssCheckBox();
            row.AddChild(check);
            check.Label.Text = "Check " + i;

            var edit = new CssLineEdit();
            row.AddChild(edit);
            edit.Text = "text " + i;
        }

        return root;
    }
}
