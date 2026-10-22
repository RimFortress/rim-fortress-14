using Content.Client._RF.Suil;

namespace Content.Client._RF.UserInterface.Controls;

/// <summary>
/// The look of an <see cref="RfWindow"/>: the panel, the header with the title and its buttons, and the area of the
/// contents. The tree is in <c>elements/common/window.suil</c>.
/// </summary>
public sealed partial class RfWindowChrome : RfBoxContainer
{
    public event Action? OnCloseRequested;
    public event Action? OnHelpRequested;

    public RfWindowChrome()
    {
        InitializeComponent();

        HelpButton.OnPressed += _ => OnHelpRequested?.Invoke();
        CloseButton.OnPressed += _ => OnCloseRequested?.Invoke();
    }

    internal UiElement Body => Contents;

    internal UiElement Title => TitleLabel;

    internal RfButton Help => HelpButton;

    internal RfButton Close => CloseButton;
}
