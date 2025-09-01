using System.Linq;
using Content.Client._RF.Lobby;
using Content.Client._RF.Lobby.UI;
using Content.Client.Humanoid;
using Content.Client.Inventory;
using Content.Client.Lobby;
using Content.Client.Station;
using Content.Shared._RF.CCVar;
using Content.Shared._RF.Preferences;
using Content.Shared.CCVar;
using Content.Shared.Clothing;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Roles;
using Content.Shared.Traits;
using Robust.Client.Player;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Rf.Lobby;

public sealed class RfLobbyUIController : UIController, IOnStateEntered<RimFortressLobbyState>, IOnStateExited<RimFortressLobbyState>
{
    [Dependency] private readonly IClientPreferencesManager _preferencesManager = default!;
    [Dependency] private readonly IConfigurationManager _configurationManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IStateManager _stateManager = default!;
    [UISystemDependency] private readonly HumanoidAppearanceSystem _humanoid = default!;
    [UISystemDependency] private readonly ClientInventorySystem _inventory = default!;
    [UISystemDependency] private readonly StationSpawningSystem _spawn = default!;

    private RfCharacterSetupGui? _characterSetup;
    private RfHumanoidProfileEditor? _profileEditor;
    private RfCharacterSetupGuiSavePanel? _savePanel;
    private RfCharacterSetupGuiSavePanel? _saveEquipmentPanel;
    private RfExpeditionEquipmentEditor? _equipmentEditor;

    /// <summary>
    /// This is the character preview panel in the chat. This should only update if their character updates.
    /// </summary>
    private RfLobbyCharacterPreviewPanel? PreviewPanel => GetLobbyPreview();

    public override void Initialize()
    {
        base.Initialize();
        _prototypeManager.PrototypesReloaded += OnProtoReload;
        _preferencesManager.OnServerDataLoaded += PreferencesDataLoaded;

        _configurationManager.OnValueChanged(CCVars.FlavorText, _ => _profileEditor?.RefreshFlavorText());
    }

    private RfLobbyCharacterPreviewPanel? GetLobbyPreview()
    {
        if (_stateManager.CurrentState is RimFortressLobbyState lobby)
            return lobby.Lobby?.CharacterPreview;

        return null;
    }

    private void OnProtoReload(PrototypesReloadedEventArgs obj)
    {
        if (obj.WasModified<SpeciesPrototype>())
            _profileEditor?.RefreshSpecies();

        if (obj.WasModified<TraitPrototype>())
            _profileEditor?.RefreshTraits();

        if (obj.WasModified<ExpeditionEquipmentPrototype>())
            _equipmentEditor?.BuildList();
    }

    private void PreferencesDataLoaded()
    {
        PreviewPanel?.SetLoaded(true);

        if (_stateManager.CurrentState is not RimFortressLobbyState)
            return;

        ReloadCharacterSetup();
    }

    public void OnStateEntered(RimFortressLobbyState state)
    {
        PreviewPanel?.SetLoaded(_preferencesManager.ServerDataLoaded);
        ReloadCharacterSetup();
    }

    public void OnStateExited(RimFortressLobbyState state)
    {
        PreviewPanel?.SetLoaded(false);
        _profileEditor?.Parent?.RemoveChild(_profileEditor);
        _characterSetup?.Parent?.RemoveChild(_characterSetup);
        _equipmentEditor?.Parent?.RemoveChild(_equipmentEditor);

        _characterSetup = null;
        _profileEditor = null;
        _equipmentEditor = null;
    }

    /// <summary>
    /// Reloads every single character setup control.
    /// </summary>
    public void ReloadCharacterSetup()
    {
        RefreshLobbyPreview();
        var (characterGui, profileEditor, equipmentEditor) = EnsureGui();

        characterGui.ReloadCharacterPickers();
        profileEditor.SetProfile((HumanoidCharacterProfile?) _characterSetup?.SelectedProfile,
            _characterSetup?.SelectedProfileIndex);
        equipmentEditor.BuildList();

        characterGui.IsDirty = false;
        profileEditor.IsDirty = false;
    }

    public int LeftPoints()
    {
        var maxPoints = _configurationManager.GetCVar(RfVars.RoundstartEquipmentPoints);
        var point = 0;

        foreach (var (slot, profile) in _characterSetup?.Profiles ?? new())
        {
            if (profile is not HumanoidCharacterProfile character)
                continue;

            if (_profileEditor?.CharacterSlot == slot)
                point += _profileEditor.Profile?.GetSkillPoints() ?? 0;
            else
                point += character.GetSkillPoints();
        }

        return maxPoints - (_equipmentEditor?.UsedPoints() ?? 0) - point;
    }

    /// <summary>
    /// Refreshes the character preview in the lobby chat.
    /// </summary>
    private void RefreshLobbyPreview()
    {
        if (PreviewPanel == null || _preferencesManager.Preferences == null)
            return;

        var prefs = _preferencesManager.Preferences.Characters
            .Take(_configurationManager.GetCVar(RfVars.MaxRoundstartPops));
        var profiles = new Dictionary<EntityUid, string>();

        foreach (var (_, profile) in prefs)
        {
            if (profile is not HumanoidCharacterProfile hum)
            {
                profiles.Add(EntityUid.Invalid, string.Empty);
                return;
            }

            profiles.Add(LoadProfileEntity(hum, null, true), hum.Name);
        }

        PreviewPanel.SetProfiles(profiles);
    }

    private void CloseProfileEditor()
    {
        if (_profileEditor == null)
            return;

        _profileEditor.SetProfile(null, null);

        if (_stateManager.CurrentState is RimFortressLobbyState lobbyGui)
        {
            lobbyGui.SwitchState(RfLobbyGui.LobbyGuiState.Default);
        }
    }

    private void OpenSavePanel()
    {
        if (_savePanel is { IsOpen: true })
            return;

        _savePanel = new RfCharacterSetupGuiSavePanel();

        _savePanel.SaveButton.OnPressed += _ =>
        {
            _characterSetup?.SaveProfiles();
            _savePanel.Close();
            CloseProfileEditor();
        };

        _savePanel.NoSaveButton.OnPressed += _ =>
        {
            _savePanel.Close();
            CloseProfileEditor();
        };

        _savePanel.OpenCentered();
    }

    private void CloseEquipmentEditor()
    {
        if (_equipmentEditor == null)
            return;

        if (_stateManager.CurrentState is RimFortressLobbyState lobbyGui)
            lobbyGui.SwitchState(RfLobbyGui.LobbyGuiState.Default);
    }

    private void OpenSaveEquipmentPanel()
    {
        if (_saveEquipmentPanel is { IsOpen: true })
            return;

        _saveEquipmentPanel = new RfCharacterSetupGuiSavePanel();

        _saveEquipmentPanel.SaveButton.OnPressed += _ =>
        {
            _equipmentEditor?.SaveSettings();
            _saveEquipmentPanel.Close();
            CloseEquipmentEditor();
        };

        _saveEquipmentPanel.NoSaveButton.OnPressed += _ =>
        {
            _equipmentEditor?.BuildList();
            _saveEquipmentPanel.Close();
            CloseEquipmentEditor();
        };

        _saveEquipmentPanel.OpenCentered();
    }

    private (RfCharacterSetupGui, RfHumanoidProfileEditor, RfExpeditionEquipmentEditor) EnsureGui()
    {
        if (_characterSetup != null
            && _profileEditor != null
            && _equipmentEditor != null)
        {
            _characterSetup.Visible = true;
            return (_characterSetup, _profileEditor, _equipmentEditor);
        }

        _profileEditor = new RfHumanoidProfileEditor();
        _profileEditor.OnDirty += UpdateSaveButton;

        _characterSetup = new RfCharacterSetupGui(_profileEditor);
        _characterSetup.CloseButton.OnPressed += _ =>
        {
            // Open the save panel if we have unsaved changes.
            if (_profileEditor.Profile != null
                && (_profileEditor.IsDirty || _characterSetup.IsDirty))
            {
                OpenSavePanel();

                return;
            }

            // Reset sliders etc.
            CloseProfileEditor();
        };

        _characterSetup.OnBeforeSave += delegate
        {
            if (_characterSetup?.SelectedProfile != null && _profileEditor?.Profile != null)
                _characterSetup.SelectedProfile = _profileEditor.Profile;
        };
        _characterSetup.OnSave += ReloadCharacterSetup;
        _characterSetup.OnDirty += UpdateSaveButton;
        _characterSetup.OnSelected += args =>
        {
            if (_profileEditor is { Profile: not null, CharacterSlot: not null }
                && _characterSetup?.Profiles != null
                && !_characterSetup.Profiles[args].MemberwiseEquals(_profileEditor.Profile))
            {
                _characterSetup.Profiles[args] = _profileEditor.Profile.Clone();
                _characterSetup.IsDirty = true;
            }

            _profileEditor?.SetProfile((HumanoidCharacterProfile?) _characterSetup?.SelectedProfile,
                _characterSetup?.SelectedProfileIndex);
        };

        _equipmentEditor = new RfExpeditionEquipmentEditor();
        _equipmentEditor.CloseButton.OnPressed += _ =>
        {
            if (_equipmentEditor is { IsDirty: true })
            {
                OpenSaveEquipmentPanel();
                return;
            }

            CloseEquipmentEditor();
        };

        if (_stateManager.CurrentState is RimFortressLobbyState lobby)
        {
            lobby.Lobby?.CharacterSetupState.AddChild(_characterSetup);
            lobby.Lobby?.EquipmentEditorState.AddChild(_equipmentEditor);
        }

        UpdateSaveButton();
        return (_characterSetup, _profileEditor, _equipmentEditor);
    }

    private void UpdateSaveButton()
    {
        if (_characterSetup == null)
            return;

        var dirty = !(_characterSetup.IsDirty || (_profileEditor?.IsDirty ?? false));
        _characterSetup.SaveProfilesButton.Disabled = dirty;
        _characterSetup.ResetButton.Disabled = dirty;
    }

    #region Helpers

    /// <summary>
    /// Gets the highest priority job for the profile.
    /// </summary>
    public JobPrototype GetPreferredJob(HumanoidCharacterProfile profile)
    {
        var highPriorityJob = profile.JobPriorities.FirstOrDefault(p => p.Value == JobPriority.High).Key;
        // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract (what is resharper smoking?)
        return _prototypeManager.Index<JobPrototype>(highPriorityJob.Id ?? SharedGameTicker.FallbackOverflowJob);
    }

    public void GiveDummyLoadout(EntityUid uid, RoleLoadout? roleLoadout)
    {
        if (roleLoadout == null)
            return;

        foreach (var group in roleLoadout.SelectedLoadouts.Values)
        {
            foreach (var loadout in group)
            {
                if (!_prototypeManager.TryIndex(loadout.Prototype, out var loadoutProto))
                    continue;

                _spawn.EquipStartingGear(uid, loadoutProto);
            }
        }
    }

    /// <summary>
    /// Applies the specified job's clothes to the dummy.
    /// </summary>
    public void GiveDummyJobClothes(EntityUid dummy, HumanoidCharacterProfile profile, JobPrototype job)
    {
        if (!_inventory.TryGetSlots(dummy, out var slots))
            return;

        // Apply loadout
        if (profile.Loadouts.TryGetValue(job.ID, out var jobLoadout))
        {
            foreach (var loadouts in jobLoadout.SelectedLoadouts.Values)
            {
                foreach (var loadout in loadouts)
                {
                    if (!_prototypeManager.TryIndex(loadout.Prototype, out var loadoutProto))
                        continue;

                    // TODO: Need some way to apply starting gear to an entity and replace existing stuff coz holy fucking shit dude.
                    foreach (var slot in slots)
                    {
                        // Try startinggear first
                        if (_prototypeManager.TryIndex(loadoutProto.StartingGear, out var loadoutGear))
                        {
                            var itemType = ((IEquipmentLoadout) loadoutGear).GetGear(slot.Name);

                            if (_inventory.TryUnequip(dummy, slot.Name, out var unequippedItem, silent: true, force: true, reparent: false))
                            {
                                EntityManager.DeleteEntity(unequippedItem.Value);
                            }

                            if (itemType != string.Empty)
                            {
                                var item = EntityManager.SpawnEntity(itemType, MapCoordinates.Nullspace);
                                _inventory.TryEquip(dummy, item, slot.Name, true, true);
                            }
                        }
                        else
                        {
                            var itemType = ((IEquipmentLoadout) loadoutProto).GetGear(slot.Name);

                            if (_inventory.TryUnequip(dummy, slot.Name, out var unequippedItem, silent: true, force: true, reparent: false))
                            {
                                EntityManager.DeleteEntity(unequippedItem.Value);
                            }

                            if (itemType != string.Empty)
                            {
                                var item = EntityManager.SpawnEntity(itemType, MapCoordinates.Nullspace);
                                _inventory.TryEquip(dummy, item, slot.Name, true, true);
                            }
                        }
                    }
                }
            }
        }

        if (!_prototypeManager.TryIndex(job.StartingGear, out var gear))
            return;

        foreach (var slot in slots)
        {
            var itemType = ((IEquipmentLoadout) gear).GetGear(slot.Name);

            if (_inventory.TryUnequip(dummy, slot.Name, out var unequippedItem, silent: true, force: true, reparent: false))
            {
                EntityManager.DeleteEntity(unequippedItem.Value);
            }

            if (itemType != string.Empty)
            {
                var item = EntityManager.SpawnEntity(itemType, MapCoordinates.Nullspace);
                _inventory.TryEquip(dummy, item, slot.Name, true, true);
            }
        }
    }

    /// <summary>
    /// Loads the profile onto a dummy entity.
    /// </summary>
    public EntityUid LoadProfileEntity(HumanoidCharacterProfile? humanoid, JobPrototype? job, bool jobClothes)
    {
        EntityUid dummyEnt;

        EntProtoId? previewEntity = null;
        if (humanoid != null && jobClothes)
        {
            job ??= GetPreferredJob(humanoid);

            previewEntity = job.JobPreviewEntity ?? (EntProtoId?)job.JobEntity;
        }

        if (previewEntity != null)
        {
            // Special type like borg or AI, do not spawn a human just spawn the entity.
            dummyEnt = EntityManager.SpawnEntity(previewEntity, MapCoordinates.Nullspace);
            return dummyEnt;
        }
        else if (humanoid is not null)
        {
            var dummy = _prototypeManager.Index(humanoid.Species).DollPrototype;
            dummyEnt = EntityManager.SpawnEntity(dummy, MapCoordinates.Nullspace);
        }
        else
        {
            dummyEnt = EntityManager.SpawnEntity(_prototypeManager.Index<SpeciesPrototype>(SharedHumanoidAppearanceSystem.DefaultSpecies).DollPrototype, MapCoordinates.Nullspace);
        }

        _humanoid.LoadProfile(dummyEnt, humanoid);

        if (humanoid != null && jobClothes)
        {
            DebugTools.Assert(job != null);

            GiveDummyJobClothes(dummyEnt, humanoid, job);

            if (_prototypeManager.HasIndex<RoleLoadoutPrototype>(LoadoutSystem.GetJobPrototype(job.ID)))
            {
                var loadout = humanoid.GetLoadoutOrDefault(LoadoutSystem.GetJobPrototype(job.ID), _playerManager.LocalSession, humanoid.Species, EntityManager, _prototypeManager);
                GiveDummyLoadout(dummyEnt, loadout);
            }
        }

        return dummyEnt;
    }

    #endregion
}
