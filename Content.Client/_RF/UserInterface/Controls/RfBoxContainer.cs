using System.Linq;
using System.Numerics;
using Content.Client._RF.Suil;
using Content.Suil;
using Robust.Client.UserInterface;

namespace Content.Client._RF.UserInterface.Controls;

/// <summary>
/// SUIL <c>flex-direction</c>.
/// </summary>
public enum SuilFlexDirection : byte
{
    Row,
    RowReverse,
    Column,
    ColumnReverse,
}

/// <summary>
/// SUIL <c>justify-content</c>: distribution of the free space on the main axis.
/// </summary>
public enum SuilJustifyContent : byte
{
    FlexStart,
    FlexEnd,
    Center,
    SpaceBetween,
    SpaceAround,
    SpaceEvenly,
}

/// <summary>
/// A single-line flex container (<c>display: flex</c> without wrapping), the <see cref="UiElement"/> version of
/// <see cref="Robust.Client.UserInterface.Controls.BoxContainer"/>.
/// </summary>
/// <remarks>
/// <para>
/// The container reads <see cref="UiElement.FlexGrow"/> and <see cref="UiElement.AlignSelf"/> of its children.
/// A child keeps its desired size, the free space is shared among children with a <c>flex-grow</c>, or, if there
/// is none, distributed by <see cref="JustifyContent"/>. When the children don't fit, they shrink in proportion
/// to their size (<c>flex-shrink: 1</c>).
/// </para>
/// <para>
/// The padding and the border of the container are respected. The container must not display text.
/// </para>
/// </remarks>
[Virtual]
[SuilTag("box")]
public partial class RfBoxContainer : UiElement
{
    [SuilProperty]
    public SuilFlexDirection FlexDirection
    {
        get;
        set
        {
            field = value;
            InvalidateMeasure();
        }
    }

    [SuilProperty]
    public SuilJustifyContent JustifyContent
    {
        get;
        set
        {
            field = value;
            InvalidateArrange();
        }
    }

    [SuilProperty]
    public SuilAlignItems AlignItems
    {
        get;
        set
        {
            field = value;
            InvalidateArrange();
        }
    }

    private bool IsRow => FlexDirection is SuilFlexDirection.Row or SuilFlexDirection.RowReverse;

    private bool IsReverse => FlexDirection is SuilFlexDirection.RowReverse or SuilFlexDirection.ColumnReverse;

    private static float MainAxis(Vector2 size, bool row) => row ? size.X : size.Y;

    private static float Cross(Vector2 size, bool row) => row ? size.Y : size.X;

    private static Vector2 Make(float main, float cross, bool row) => row ? new Vector2(main, cross) : new Vector2(cross, main);

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var row = IsRow;
        var inset = ContentInset;
        var insetSize = new Vector2(inset.Left + inset.Right, inset.Top + inset.Bottom);
        var available = Vector2.Max(availableSize - insetSize, Vector2.Zero);
        var gap = MathF.Max(0f, ToPx(Gap));

        var remaining = MainAxis(available, row);
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
                remaining -= MainAxis(desired, row);
                mainSize += MainAxis(desired, row);
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
        var mainContent = MainAxis(content, row);
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
            sizes[i] = MainAxis(items[i].DesiredSize, row);
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
            sizes = ShareFreeSpace(items, sizes, mainContent - gap * (count - 1), row);
        }
        else
        {
            switch (JustifyContent)
            {
                case SuilJustifyContent.FlexEnd:
                    offset = free;
                    break;
                case SuilJustifyContent.Center:
                    offset = free / 2f;
                    break;
                case SuilJustifyContent.SpaceBetween when count > 1:
                    spacing += free / (count - 1);
                    break;
                case SuilJustifyContent.SpaceAround:
                    spacing += free / count;
                    offset = free / count / 2f;
                    break;
                case SuilJustifyContent.SpaceEvenly:
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
            var crossSize = align == SuilAlignSelf.Stretch ? crossContent : MathF.Min(crossDesired, crossContent);
            var crossPos = align switch
            {
                SuilAlignSelf.FlexEnd => crossContent - crossSize,
                SuilAlignSelf.Center => (crossContent - crossSize) / 2f,
                _ => 0f,
            };

            child.Arrange(UIBox2.FromDimensions(origin + Make(mainPos, crossPos, row), Make(size, crossSize, row)));

            offset += size + spacing;
        }

        return finalSize;
    }

    /// <summary>
    /// Shares the space among the children by <c>flex-grow</c>. The size of a child starts from its content, or from
    /// nothing with <c>flex-basis: zero</c>; it is never less than its content, and what a child that has more in it takes
    /// over its share is taken from the others.
    /// </summary>
    private static float[] ShareFreeSpace(List<Control> items, float[] desired, float space, bool row)
    {
        var count = items.Count;
        var target = new float[count];
        var frozen = new bool[count];
        var grows = new float[count];
        var bases = new float[count];

        for (var i = 0; i < count; i++)
        {
            grows[i] = MathF.Max(0f, GetFlexGrow(items[i], row));
            bases[i] = items[i] is UiElement { FlexBasis: SuilFlexBasis.Zero } && grows[i] > 0f ? 0f : desired[i];
        }

        for (var round = 0; round <= count; round++)
        {
            var pool = space;
            var poolGrow = 0f;

            for (var i = 0; i < count; i++)
            {
                if (frozen[i])
                {
                    pool -= target[i];
                    continue;
                }

                pool -= bases[i];
                poolGrow += grows[i];
            }

            var violated = false;

            for (var i = 0; i < count; i++)
            {
                if (frozen[i])
                    continue;

                target[i] = bases[i] + (poolGrow > 0f ? pool * grows[i] / poolGrow : 0f);

                if (target[i] < desired[i])
                {
                    target[i] = desired[i];
                    frozen[i] = true;
                    violated = true;
                }
            }

            if (!violated)
                break;
        }

        return target;
    }

    /// <summary>
    /// Can the child be made smaller than its desired size without breaking it: it clips its content or fills the
    /// free space of the row.
    /// </summary>
    private static bool CanShrink(Control child, bool row)
    {
        if (child is not UiElement suil)
            return false;

        return suil.FlexGrow > 0f || (row ? suil.OverflowX : suil.OverflowY) != SuilOverflow.Visible;
    }

    private SuilAlignSelf ResolveAlign(Control child, bool row)
    {
        var self = GetAlignSelf(child, row);
        if (self != SuilAlignSelf.Auto)
            return self;

        return AlignItems switch
        {
            SuilAlignItems.FlexStart => SuilAlignSelf.FlexStart,
            SuilAlignItems.FlexEnd => SuilAlignSelf.FlexEnd,
            SuilAlignItems.Center => SuilAlignSelf.Center,
            _ => SuilAlignSelf.Stretch,
        };
    }
}
