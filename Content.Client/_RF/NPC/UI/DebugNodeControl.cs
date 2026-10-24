using System.Linq;
using Content.Client._RF.Stylesheets;
using Content.Client._RF.UserInterface.Controls;
using JetBrains.Annotations;
using Robust.Client.Graphics;

namespace Content.Client._RF.NPC.UI;

/// <summary>
/// A node of a debug graph: the frame (see the <c>debug-node</c> template) that is outlined when the mouse is over it,
/// and the words that the search of the graph looks for in it.
/// </summary>
public abstract class DebugNodeControl : RfBoxContainer
{
    private bool _selected;

    protected HashSet<string> SearchText = new();

    protected DebugNodeControl()
    {
        OnMouseEntered += _ => _selected = true;
        OnMouseExited += _ => _selected = false;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        if (_selected)
        {
            var box = UIBox2.FromDimensions(-Vector2i.One, PixelSize + Vector2i.One * 2);
            handle.DrawRect(box, StyleFortress.GoldFortress, filled: false);
        }

        base.Draw(handle);
    }

    [PublicAPI]
    public bool SearchValid(string searchText)
        => SearchText.Any(x => x.Contains(searchText, StringComparison.InvariantCultureIgnoreCase));
}
