using Content.Client.ContextMenu.UI;
using Content.Client.Stylesheets;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._RF.UserInterface.Controls;

/// <summary>
/// Fluent builder over the engine's <see cref="ContextMenuUIController"/>/<see cref="ContextMenuPopup"/>/
/// <see cref="ContextMenuElement"/> stack - the same one used by the entity/verb menu.
/// </summary>
public sealed class ContextMenuBuilder
{
    private readonly ContextMenuUIController _controller;
    private readonly Control _windowRoot;
    private readonly ContextMenuPopup _root;
    private readonly ContextMenuPopup _menu;

    private ContextMenuBuilder(
        ContextMenuUIController controller,
        Control windowRoot,
        ContextMenuPopup root,
        ContextMenuPopup menu)
    {
        _controller = controller;
        _windowRoot = windowRoot;
        _root = root;
        _menu = menu;
    }

    /// <summary>
    /// Starts a new menu tree attached under <paramref name="windowRoot"/> rather than the shared
    /// <see cref="ContextMenuUIController.RootMenu"/>. Pass the debugger control's own <c>.Root</c>
    /// (the top-most control of its OS window's tree) so the popup lives in the debugger window instead
    /// of the main game window.
    /// </summary>
    public static ContextMenuBuilder New(Control windowRoot)
    {
        var controller = windowRoot.UserInterfaceManager.GetUIController<ContextMenuUIController>();
        var root = new ContextMenuPopup(controller, null, windowRoot);
        root.OnPopupHide += root.Dispose; // one-shot tree, unlike the persistent shared RootMenu
        return new ContextMenuBuilder(controller, windowRoot, root, root);
    }

    private static ContextMenuBuilder ForSubMenu(
        ContextMenuUIController controller,
        Control windowRoot,
        ContextMenuPopup root,
        ContextMenuElement parent)
    {
        // Must pass windowRoot explicitly here too - otherwise this submenu falls back to the default
        // ModalRoot and you get the exact same "wrong window" bug one level deeper.
        var popup = new ContextMenuPopup(controller, parent, windowRoot);
        return new ContextMenuBuilder(controller, windowRoot, root, popup);
    }

    /// <summary>
    /// Adds a single clickable leaf entry. Clicking it invokes <paramref name="onClick"/> and closes the
    /// entire menu, mirroring how executing a verb closes the verb menu.
    /// </summary>
    public ContextMenuBuilder Item(string text, Action onClick)
    {
        var element = new ContextMenuElement(text);
        element.OnPressed += _ =>
        {
            onClick();
            _controller.Close();
        };

        _controller.AddElement(_menu, element);
        return this;
    }

    /// <summary>
    /// Adds a nested submenu, configured via a scoped child builder.
    /// </summary>
    public ContextMenuBuilder Submenu(string text, Action<ContextMenuBuilder> configure)
    {
        var element = new ContextMenuElement(text);
        _controller.AddElement(_menu, element);

        var child = ForSubMenu(_controller, _windowRoot, _root, element);
        configure(child);
        return this;
    }

    /// <summary>
    /// Projects a source sequence into leaf entries.
    /// </summary>
    public ContextMenuBuilder Items<T>(
        IEnumerable<T> source,
        Func<T, string> text,
        Action<T, int> onClick)
    {
        var index = 0;
        foreach (var item in source)
        {
            var captured = item;
            var i = index++;
            Item(text(captured), () => onClick(captured, i));
        }

        return this;
    }

    /// <inheritdoc cref="Items{T}(System.Collections.Generic.IEnumerable{T},System.Func{T,string},System.Action{T,int})"/>
    public ContextMenuBuilder Items<T>(
        IEnumerable<T> source,
        Func<T, string> text,
        Action<T> onClick)
        => Items(source, text, (b, _) => onClick(b));

    /// <summary>
    /// Projects a source sequence into nested submenus, one per item, with the item's index available -
    /// avoids repeated <c>IndexOf</c> lookups at the call site.
    /// </summary>
    public ContextMenuBuilder Submenus<T>(
        IEnumerable<T> source,
        Func<T, string> text,
        Action<ContextMenuBuilder, T, int> configure)
    {
        var index = 0;
        foreach (var item in source)
        {
            var captured = item;
            var i = index++;
            Submenu(text(captured), b => configure(b, captured, i));
        }

        return this;
    }

    /// <inheritdoc cref="Submenus{T}(IEnumerable{T}, Func{T, string}, Action{ContextMenuBuilder, T, int})"/>
    public ContextMenuBuilder Submenus<T>(
        IEnumerable<T> source,
        Func<T, string> text,
        Action<ContextMenuBuilder, T> configure)
        => Submenus(source, text, (b, item, _) => configure(b, item));

    /// <summary>
    /// Conditionally applies a chunk of configuration.
    /// </summary>
    public ContextMenuBuilder When(bool condition, Action<ContextMenuBuilder> configure)
    {
        if (condition)
            configure(this);

        return this;
    }

    /// <summary>
    /// Adds a non-interactive divider row. Added directly to <see cref="ContextMenuPopup.MenuBody"/>, bypassing
    /// <see cref="ContextMenuUIController.AddElement"/> since it isn't a <see cref="ContextMenuElement"/> and
    /// needs no hover/click wiring.
    /// </summary>
    public ContextMenuBuilder Separator()
    {
        _menu.MenuBody.AddChild(new PanelContainer
        {
            StyleClasses = { StyleClass.LowDivider },
            Margin = new Thickness(3f, 2f),
        });
        return this;
    }

    /// <summary>
    /// Finalizes the tree and returns the root <see cref="ContextMenuPopup"/>, ready to be positioned via
    /// <see cref="Popup.Open"/> and pushed onto <see cref="ContextMenuUIController.Menus"/>.
    /// </summary>
    public ContextMenuPopup Build()
    {
        _controller.Menus.Push(_menu);
        return _menu;
    }
}
