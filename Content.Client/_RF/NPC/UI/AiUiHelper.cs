using Content.Client._RF.NPC.GOAP.UI;
using Content.Client._RF.Stylesheets;
using Content.Shared._RF.NPC;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._RF.NPC.UI;

public static class AiUiHelper
{
    public static void AddTypes(Control parent, string title, Dictionary<string, (string, string)>? data)
    {
        if (data == null || data.Count == 0)
            return;

        AddSeparator(parent);
        var expandBox = new ExpandableBox { Title = $"[bold]{title}[/bold]" };
        parent.AddChild(expandBox);

        foreach (var (key, (type, value)) in data)
        {
            AddSeparator(expandBox.Content);
            expandBox.Content.AddChild(new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                Margin = new Thickness(0f, 1f),
                Children =
                {
                    new BoxContainer
                    {
                        Children =
                        {
                            new RichTextLabel { Text = $"[bold]{key}[/bold]" },
                            new Control { HorizontalExpand = true },
                            new RichTextLabel { Text = $":{type}" },
                        },
                    },
                    new BoxContainer
                    {
                        Children =
                        {
                            new RichTextLabel { Text = value },
                        },
                    },
                },
            });
        }
    }

    public static void AddTypes(Control parent, string? title, ObjectDebugReflection node)
    {
        var header = title != null
            ? $"[bold]{title}[/bold]"
            : (string.IsNullOrEmpty(node.Name)
                ? $"[bold]{node.TypeName}[/bold]"
                : $"[bold]{node.Name}[/bold] ({node.TypeName})");

        AddSeparator(parent);
        var expandBox = new ExpandableBox { Title = header };
        parent.AddChild(expandBox);

        if (node.Fields.Count > 0)
        {
            foreach (var (key, (type, value)) in node.Fields)
            {
                AddSeparator(expandBox.Content);
                expandBox.Content.AddChild(new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Vertical,
                    Margin = new Thickness(0f, 1f),
                    Children =
                    {
                        new BoxContainer
                        {
                            Children =
                            {
                                new RichTextLabel { Text = $"[bold]{key}[/bold]" },
                                new Control { HorizontalExpand = true },
                                new RichTextLabel { Text = $":{type}" },
                            },
                        },
                        new BoxContainer
                        {
                            Children =
                            {
                                new RichTextLabel { Text = value },
                            },
                        },
                    },
                });
            }
        }

        if (node.Children.Count <= 0)
            return;

        foreach (var child in node.Children)
        {
            AddTypes(expandBox.Content, null, child);
        }
    }

    public static void AddLabel(Control parent, string left, string right)
    {
        AddSeparator(parent);
        parent.AddChild(new BoxContainer
        {
            Margin = new Thickness(20f, 0f, 5f, 0f),
            Children =
            {
                new RichTextLabel { Text = $"[bold]{left}[/bold]" },
                new Control { HorizontalExpand = true },
                new RichTextLabel { Text = $"[bold]{right}[/bold]" },
            },
        });
    }

    public static void AddExpandLabel(Control parent, string title, string text)
    {
        AddSeparator(parent);
        var expandBox = new ExpandableBox { Title = $"[bold]{title}[/bold]" };
        parent.AddChild(expandBox);
        expandBox.Content.AddChild(new RichTextLabel { Text = text });
    }

    public static void AddSeparator(Control parent)
    {
        parent.AddChild(new PanelContainer
        {
            StyleClasses = { StyleFortress.StyleClassLowDividerDark },
            Margin = new Thickness(3f, 2f),
        });
    }

    public static void AddLogs(Control parent, string? logs)
    {
        if (string.IsNullOrWhiteSpace(logs))
            return;

        AddSeparator(parent);
        var box = new ExpandableBox { Title = "[bold]Logs[/bold]" };
        parent.AddChild(box);
        var dumps = logs.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var dump in dumps)
        {
            box.Content.AddChild(new RichTextLabel { Text = dump });
        }
    }

    public static ExpandableBox AddBox(Control parent, string title, string? second = null)
    {
        AddSeparator(parent);
        var box = new ExpandableBox
        {
            Title = $"[bold]{title}[/bold]",
            SecondaryText = second != null ? $"[bold]{second}[/bold]" : null,
        };
        parent.AddChild(box);
        return box;
    }
}
