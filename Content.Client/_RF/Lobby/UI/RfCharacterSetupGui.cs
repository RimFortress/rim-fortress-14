using Content.Suil;
using Content.Client._RF.UserInterface;
using System.Linq;
using Content.Client._RF.Suil;
using Content.Client._RF.UserInterface.Controls;
using Content.Client.Info;
using Content.Client.Info.PlaytimeStats;
using Content.Client.Lobby;
using Content.Shared._RF.CCVar;
using Content.Shared.CCVar;
using Content.Shared.Preferences;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Utility;

namespace Content.Client._RF.Lobby.UI;

/// <summary>
/// Holds the entire character setup GUI, from character picks to individual character editing.
/// </summary>
[SuilTag("character-setup-gui")]
public sealed partial class RfCharacterSetupGui : RfBoxContainer
{
    [Dependency] private IClientPreferencesManager _preferencesManager = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    private readonly RfButton _createNewCharacterButton;

    public event Action<int>? OnSelected;
    public event Action? OnDirty;
    public event Action? OnBeforeSave;
    public event Action? OnSave;

    public Dictionary<int, HumanoidCharacterProfile>? Profiles;
    public int SelectedProfileIndex = -1;

    public HumanoidCharacterProfile? SelectedProfile
    {
        set
        {
            if (Profiles == null
                || SelectedProfileIndex == -1
                || SelectedProfileIndex > Profiles.Count)
                return;

            Profiles[SelectedProfileIndex] = value!;
        }
        get
        {
            if (Profiles == null
                || SelectedProfileIndex == -1
                || SelectedProfileIndex > Profiles.Count)
                return null;

            return Profiles[SelectedProfileIndex];
        }
    }

    private bool _isDirty;

    public bool IsDirty
    {
        get => _isDirty;
        set
        {
            _isDirty = value;
            OnDirty?.Invoke();
        }
    }

    public RfCharacterSetupGui(RfHumanoidProfileEditor profileEditor)
    {
        InitializeComponent();
        IoCManager.InjectDependencies(this);

        Profiles = _preferencesManager.Preferences?.Characters.ToDictionary();

        _createNewCharacterButton = new RfButton
        {
            Text = Loc.GetString("character-setup-gui-create-new-character-button"),
        };

        _createNewCharacterButton.OnPressed += args =>
        {
            if (Profiles == null || _preferencesManager.Settings == null)
                return;

            var lowest = Enumerable.Range(0, _preferencesManager.Settings.MaxCharacterSlots)
                .Except(Profiles.Keys)
                .FirstOrNull();

            if (lowest == null)
                return;

            Profiles?.Add(lowest.Value, HumanoidCharacterProfile.Random());
            ReloadCharacterPickers();
            args.Event.Handle();
            IsDirty = true;
        };

        CharEditor.AddChild(profileEditor);

        _cfg.OnValueChanged(CCVars.SeeOwnNotes, p => AdminRemarksButton.Visible = p, true);
    }

    private void OpenRules() => new RulesAndInfoWindow().Open();

    private void OpenStats() => new PlaytimeStatsWindow().OpenCentered();

    private void ResetProfiles()
    {
        Profiles = null;
        OnSave?.Invoke();
    }

    /// <summary>
    /// Disposes and reloads all character picker buttons from the preferences data.
    /// </summary>
    public void ReloadCharacterPickers()
    {
        _createNewCharacterButton.Orphan();
        Characters.DisposeAllChildren();

        var numberOfFullSlots = 0;
        var characterButtonsGroup = new RfButtonGroup();

        if (!_preferencesManager.ServerDataLoaded
            || _preferencesManager.Preferences == null)
            return;

        Profiles ??= _preferencesManager.Preferences.Characters.ToDictionary();

        _createNewCharacterButton.ToolTip =
            Loc.GetString("character-setup-gui-create-new-character-button-tooltip",
                ("maxCharacters", _preferencesManager.Settings!.MaxCharacterSlots));

        (RfCharacterPickerButton Button, int Slot)? prevSlot = null;
        var maxPops = _cfg.GetCVar(RfVars.MaxRoundstartPops);
        var separatorAdded = false;

        foreach (var (slot, character) in Profiles)
        {
            numberOfFullSlots++;
            var characterPickerButton = new RfCharacterPickerButton(
                characterButtonsGroup,
                character,
                slot == SelectedProfileIndex);

            if (prevSlot is { } prev)
            {
                characterPickerButton.Up.Disabled = false;
                prev.Button.Down.Disabled = false;

                characterPickerButton.Up.OnPressed += _ => SwitchCharacter(prev.Slot, slot);
                prev.Button.Down.OnPressed += _ => SwitchCharacter(prev.Slot, slot);
            }

            prevSlot = (characterPickerButton, slot);

            if (numberOfFullSlots > maxPops && !separatorAdded)
            {
                var divider = new UiElement { Margin = new Thickness(0, 10f) };
                divider.AddSuilStyleClass(SuilClass.HighDivider);
                Characters.AddChild(divider);

                separatorAdded = true;
            }

            Characters.AddChild(characterPickerButton);

            characterPickerButton.OnPressed += _ =>
            {
                var old = SelectedProfileIndex;
                SelectedProfileIndex = slot;
                OnSelected?.Invoke(old);
                ReloadCharacterPickers();
            };

            characterPickerButton.OnDeletePressed += () =>
            {
                Profiles.Remove(slot);

                if (SelectedProfileIndex == slot)
                    SelectedProfileIndex = -1;

                IsDirty = true;
                ReloadCharacterPickers();
            };
        }

        _createNewCharacterButton.Disabled = numberOfFullSlots >= _preferencesManager.Settings.MaxCharacterSlots;
        CharactersCount.Text =
            $"{Loc.GetString("lobby-characters-preview-panel-header")} ({numberOfFullSlots}/{maxPops}):";
        Characters.AddChild(_createNewCharacterButton);
    }

    public void SaveProfiles()
    {
        if (Profiles == null)
            return;

        OnBeforeSave?.Invoke();

        foreach (var (slot, profile) in Profiles)
        {
            if (_preferencesManager.Preferences!.Characters.ContainsKey(slot))
                _preferencesManager.UpdateCharacter(profile, slot);
            else
                _preferencesManager.CreateCharacter(profile);
        }

        foreach (var (slot, _) in _preferencesManager.Preferences!.Characters)
        {
            if (!Profiles.ContainsKey(slot))
                _preferencesManager.DeleteCharacter(slot);
        }

        _preferencesManager.SelectCharacter(SelectedProfileIndex);
        OnSave?.Invoke();
    }

    private void SwitchCharacter(int prev, int next)
    {
        if (prev == next || prev == -1 || next == -1 || Profiles == null)
            return;

        if (SelectedProfileIndex == prev)
            SelectedProfileIndex = next;
        else if (SelectedProfileIndex == next)
            SelectedProfileIndex = prev;

        (Profiles[prev], Profiles[next]) = (Profiles[next], Profiles[prev]);
        ReloadCharacterPickers();
        IsDirty = true;
    }
}
