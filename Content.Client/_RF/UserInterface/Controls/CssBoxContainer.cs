using System.Linq;
using System.Numerics;
using Content.Client._RF.CSS;
using Robust.Client.UserInterface;

namespace Content.Client._RF.UserInterface.Controls;

/// <summary>
/// CSS <c>flex-direction</c>.
/// </summary>
public enum CssFlexDirection : byte
{
    Row,
    RowReverse,
    Column,
    ColumnReverse,
}

/// <summary>
/// CSS <c>justify-content</c>: distribution of the free space on the main axis.
/// </summary>
public enum CssJustifyContent : byte
{
    FlexStart,
    FlexEnd,
    Center,
    SpaceBetween,
    SpaceAround,
    SpaceEvenly,
}

/// <summary>
/// A single-line flex container (<c>display: flex</c> without wrapping), the <see cref="CssControl"/> version of
/// <see cref="Robust.Client.UserInterface.Controls.BoxContainer"/>.
/// </summary>
/// <remarks>
/// <para>
/// The container reads <see cref="CssControl.FlexGrow"/> and <see cref="CssControl.AlignSelf"/> of its children.
/// A child keeps its desired size, the free space is shared among children with a <c>flex-grow</c>, or, if there
/// is none, distributed by <see cref="JustifyContent"/>. When the children don't fit, they shrink in proportion
/// to their size (<c>flex-shrink: 1</c>).
/// </para>
/// <para>
/// The padding and the border of the container are respected. The container must not display text.
/// </para>
/// </remarks>
[Virtual]
public partial class CssBoxContainer : CssControl
{
    [CssProperty]
    public CssFlexDirection FlexDirection
    {
        get;
        set
        {
            field = value;
            InvalidateMeasure();
        }
    }

    [CssProperty]
    public CssJustifyContent JustifyContent
    {
        get;
        set
        {
            field = value;
            InvalidateArrange();
        }
    }

    [CssProperty]
    public CssAlignItems AlignItems
    {
        get;
        set
        {
            field = value;
            InvalidateArrange();
        }
    }

    private bool IsRow => FlexDirection is CssFlexDirection.Row or CssFlexDirection.RowReverse;

    private bool IsReverse => FlexDirection is CssFlexDirection.RowReverse or CssFlexDirection.ColumnReverse;

    private static float Main(Vector2 size, bool row) => row ? size.X : size.Y;

    private static float Cross(Vector2 size, bool row) => row ? size.Y : size.X;

    private static Vector2 Make(float main, float cross, bool row) => row ? new Vector2(main, cross) : new Vector2(cross, main);

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var row = IsRow;
        var inset = ContentInset;
        var insetSize = new Vector2(inset.Left + inset.Right, inset.Top + inset.Bottom);
        var available = Vector2.Max(availableSize - insetSize, Vector2.Zero);
        var gap = MathF.Max(0f, ToPx(Gap));

        var remaining = Main(available, row);
        var crossAvailable = Cross(available, row);
        var mainSize = 0f;
        var crossSize = 0f;
        var count = 0;

        // Children that can't be squeezed are measured first, so a squeezable one (a clipped label that grows to
        // fill the row) can't take the space they need: the rest of the row goes to the squeezable children.
        var visible = Children.Where(child => child.Visible).ToList();
        remaining -= gap * Math.Max(0, visible.Count - 1);
        mainSize += gap * Math.Max(0, visible.Count - 1);

        for (var pass = 0; pass < 2; pass++)
        {
            foreach (var child in visible)
            {
                if (CanShrink(child, row) != (pass == 1))
                    continue;

                child.Measure(Make(MathF.Max(0f, remaining), crossAvailable, row));

                var desired = child.DesiredSize;
                remaining -= Main(desired, row);
                mainSize += Main(desired, row);
                crossSize = MathF.Max(crossSize, Cross(desired, row));
                count++;
            }
        }

        return Make(mainSize, crossSize, row) + insetSize;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var row = IsRow;
        var inset = ContentInset;
        var origin = new Vector2(inset.Left, inset.Top);
        var content = Vector2.Max(finalSize - new Vector2(inset.Left + inset.Right, inset.Top + inset.Bottom), Vector2.Zero);
        var mainContent = Main(content, row);
        var crossContent = Cross(content, row);
        var gap = MathF.Max(0f, ToPx(Gap));

        var items = new List<Control>(ChildCount);
        foreach (var child in Children)
        {
            if (child.Visible)
                items.Add(child);
        }

        if (IsReverse)
            items.Reverse();

        var count = items.Count;
        if (count == 0)
            return finalSize;

        var sizes = new float[count];
        var desiredSum = 0f;
        var growSum = 0f;
        for (var i = 0; i < count; i++)
        {
            sizes[i] = Main(items[i].DesiredSize, row);
            desiredSum += sizes[i];
            growSum += MathF.Max(0f, GetFlexGrow(items[i], row));
        }

        var free = mainContent - desiredSum - gap * (count - 1);
        var offset = 0f;
        var spacing = gap;

        if (free < 0f)
        {
            // flex-shrink: the children that can be squeezed give up their size first, the others keep theirs
            // (a button doesn't lose its padding because a long label is next to it).
            var deficit = -free;
            var shrinkableSum = 0f;

            for (var i = 0; i < count; i++)
            {
                if (CanShrink(items[i], row))
                    shrinkableSum += sizes[i];
            }

            if (shrinkableSum >= deficit)
            {
                var factor = (shrinkableSum - deficit) / shrinkableSum;

                for (var i = 0; i < count; i++)
                {
                    if (CanShrink(items[i], row))
                        sizes[i] *= factor;
                }
            }
            else
            {
                // Nothing left to squeeze there, so everything is squeezed proportionally to its size.
                var factor = desiredSum > 0f ? MathF.Max(0f, (mainContent - gap * (count - 1)) / desiredSum) : 0f;

                for (var i = 0; i < count; i++)
                {
                    sizes[i] *= factor;
                }
            }
        }
        else if (growSum > 0f)
        {
            for (var i = 0; i < count; i++)
            {
                sizes[i] += free * MathF.Max(0f, GetFlexGrow(items[i], row)) / growSum;
            }
        }
        else
        {
            switch (JustifyContent)
            {
                case CssJustifyContent.FlexEnd:
                    offset = free;
                    break;
                case CssJustifyContent.Center:
                    offset = free / 2f;
                    break;
                case CssJustifyContent.SpaceBetween when count > 1:
                    spacing += free / (count - 1);
                    break;
                case CssJustifyContent.SpaceAround:
                    spacing += free / count;
                    offset = free / count / 2f;
                    break;
                case CssJustifyContent.SpaceEvenly:
                    spacing += free / (count + 1);
                    offset = free / (count + 1);
                    break;
            }
        }

        var reverse = IsReverse;
        for (var i = 0; i < count; i++)
        {
            var child = items[i];
            var size = sizes[i];
            var mainPos = reverse ? mainContent - offset - size : offset;

            var align = ResolveAlign(child, row);
            var crossDesired = Cross(child.DesiredSize, row);
            var crossSize = align == CssAlignSelf.Stretch ? crossContent : MathF.Min(crossDesired, crossContent);
            var crossPos = align switch
            {
                CssAlignSelf.FlexEnd => crossContent - crossSize,
                CssAlignSelf.Center => (crossContent - crossSize) / 2f,
                _ => 0f,
            };

            child.Arrange(UIBox2.FromDimensions(origin + Make(mainPos, crossPos, row), Make(size, crossSize, row)));

            offset += size + spacing;
        }

        return finalSize;
    }

    /// <summary>
    /// Can the child be made smaller than its desired size without breaking it: it clips its content or fills the
    /// free space of the row.
    /// </summary>
    private static bool CanShrink(Control child, bool row)
    {
        if (child is not CssControl css)
            return false;

        return css.FlexGrow > 0f || (row ? css.OverflowX : css.OverflowY) != CssOverflow.Visible;
    }

    private CssAlignSelf ResolveAlign(Control child, bool row)
    {
        var self = GetAlignSelf(child, row);
        if (self != CssAlignSelf.Auto)
            return self;

        return AlignItems switch
        {
            CssAlignItems.FlexStart => CssAlignSelf.FlexStart,
            CssAlignItems.FlexEnd => CssAlignSelf.FlexEnd,
            CssAlignItems.Center => CssAlignSelf.Center,
            _ => CssAlignSelf.Stretch,
        };
    }
}
