using Content.Suil;
using Content.Client._RF.Skills;
using Content.Client._RF.UserInterface.Controls;
using Content.Shared.Preferences;

namespace Content.Client._RF.Lobby.UI;

/// <summary>
/// Holds character data on the side of the setup GUI.
/// </summary>
public sealed partial class RfCharacterPickerButton : RfButton
{
    [Dependency] private IEntityManager _entManager = default!;

    private EntityUid _previewDummy;

    /// <summary>
    /// Invoked if we should delete the attached character
    /// </summary>
    public event Action? OnDeletePressed;

    public RfCharacterPickerButton(RfButtonGroup group, HumanoidCharacterProfile profile, bool isSelected)
    {
        IoCManager.InjectDependencies(this);
        InitializeComponent();

        ToggleMode = true;
        Group = group;
        var description = profile.Name;

        _previewDummy = UserInterfaceManager
            .GetUIController<RfLobbyUIController>()
            .LoadProfileEntity(profile, null, true);

        description = $"{description}, \n{_entManager.System<SkillsSystem>().SkillProfession(profile.SkillsPreferences)}";

        Pressed = isSelected;
        DeleteButton.Disabled = isSelected;

        View.SetEntity(_previewDummy);
        // The name of the character is not markup.
        DescriptionLabel.SetPlainText(description);

    }

    private void OnDeleteConfirmed()
    {
        Parent?.RemoveChild(this);
        OnDeletePressed?.Invoke();
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();

        _entManager.DeleteEntity(_previewDummy);
        _previewDummy = default;
    }
}
