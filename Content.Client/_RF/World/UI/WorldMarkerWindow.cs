using Content.Client._RF.UserInterface.Controls;

namespace Content.Client._RF.World.UI;

public sealed partial class WorldMarkerWindow : RfWindow
{
    public event Action<(string Text, Color Color)>? OnSubmit;

    public WorldMarkerWindow()
    {
        InitializeComponent();
        ColorPanel.BackgroundColor = ColorSelectors.Color;

        SubmitButton.OnPressed += _ =>
        {
            OnSubmit?.Invoke((TextEdit.Text, ColorSelectors.Color));
            Close();
        };

        ColorSelectors.OnColorChanged += color => ColorPanel.BackgroundColor = color;
    }
}
