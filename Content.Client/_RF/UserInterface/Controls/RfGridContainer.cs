using Content.Suil;
using System.Numerics;
using Content.Client._RF.Suil;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._RF.UserInterface.Controls;

/// <summary>
/// Lays its children out in rows of <see cref="Columns"/> cells of the same width, the <see cref="UiElement"/>
/// version of <see cref="Robust.Client.UserInterface.Controls.GridContainer"/> (<c>display: grid</c> with
/// <c>grid-template-columns: repeat(N, 1fr)</c>).
/// </summary>
/// <remarks>
/// The height of a row is the height of its tallest child. The space between the cells is <c>gap</c> (both axes),
/// the padding and the border of the container are respected. A child that is not visible takes no cell, so
/// hiding the children (a search) closes the gaps.
/// </remarks>
[Virtual, UsedImplicitly]
[SuilTag("grid")]
public class RfGridContainer : UiElement
{
    /// <summary>
    /// The number of cells in a row, at least 1.
    /// </summary>
    [PublicAPI]
    public int Columns
    {
        get;
        set
        {
            value = Math.Max(1, value);
            if (field == value)
                return;

            field = value;
            InvalidateMeasure();
        }
    } = 1;

    private List<Control> VisibleChildren()
    {
        var children = new List<Control>(ChildCount);
        foreach (var child in Children)
        {
            if (child.Visible)
                children.Add(child);
        }

        return children;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var inset = ContentInset;
        var insetSize = new Vector2(inset.Left + inset.Right, inset.Top + inset.Bottom);
        var gap = MathF.Max(0f, ToPx(Gap));
        var children = VisibleChildren();
        var columns = Math.Min(Columns, Math.Max(1, children.Count));

        var cellWidth = float.IsInfinity(availableSize.X)
            ? float.PositiveInfinity
            : MathF.Max(0f, (availableSize.X - insetSize.X - gap * (columns - 1)) / columns);

        var widest = 0f;
        var height = 0f;
        var rows = 0;

        for (var start = 0; start < children.Count; start += columns)
        {
            var rowHeight = 0f;
            for (var i = start; i < Math.Min(start + columns, children.Count); i++)
            {
                children[i].Measure(new Vector2(cellWidth, float.PositiveInfinity));
                widest = MathF.Max(widest, children[i].DesiredSize.X);
                rowHeight = MathF.Max(rowHeight, children[i].DesiredSize.Y);
            }

            height += rowHeight;
            rows++;
        }

        height += gap * Math.Max(0, rows - 1);
        var width = float.IsInfinity(cellWidth) ? widest * columns + gap * (columns - 1) : availableSize.X - insetSize.X;

        return new Vector2(width, height) + insetSize;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var inset = ContentInset;
        var content = Vector2.Max(finalSize - new Vector2(inset.Left + inset.Right, inset.Top + inset.Bottom), Vector2.Zero);
        var gap = MathF.Max(0f, ToPx(Gap));
        var children = VisibleChildren();
        var columns = Math.Min(Columns, Math.Max(1, children.Count));
        var cellWidth = MathF.Max(0f, (content.X - gap * (columns - 1)) / columns);

        var y = inset.Top;
        for (var start = 0; start < children.Count; start += columns)
        {
            var rowHeight = 0f;
            for (var i = start; i < Math.Min(start + columns, children.Count); i++)
            {
                rowHeight = MathF.Max(rowHeight, children[i].DesiredSize.Y);
            }

            for (var i = start; i < Math.Min(start + columns, children.Count); i++)
            {
                var x = inset.Left + (i - start) * (cellWidth + gap);
                children[i].Arrange(UIBox2.FromDimensions(x, y, cellWidth, rowHeight));
            }

            y += rowHeight + gap;
        }

        return finalSize;
    }
}
