using Content.Suil;
using Content.Client._RF.UserInterface.Controls;
using Content.Client.Humanoid;
using Content.Client.Lobby;
using Content.Client.Sprite;
using Content.Shared.CCVar;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Preferences;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.Utility;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Direction = Robust.Shared.Maths.Direction;

namespace Content.Client._RF.Lobby.UI
{
    public sealed partial class RfHumanoidProfileEditor : RfBoxContainer
    {
        [Dependency] private IClientPreferencesManager _preferencesManager = default!;
        [Dependency] private IConfigurationManager _cfgManager = default!;
        [Dependency] private IEntityManager _entManager = default!;
        [Dependency] private IFileDialogManager _dialogManager = default!;
        [Dependency] private IPlayerManager _playerManager = default!;
        [Dependency] private IPrototypeManager _prototypeManager = default!;
        [Dependency] private IResourceManager _resManager = default!;
        [Dependency] private MarkingManager _markingManager = default!;
        [Dependency] private ILogManager _logManager = default!;

        /// <summary>
        /// The character slot for the current profile.
        /// </summary>
        public int? CharacterSlot;

        /// <summary>
        /// The work in progress profile being edited.
        /// </summary>
        public HumanoidCharacterProfile? Profile;

        public HumanoidCharacterProfile? OriginalProfile;

        private Direction _previewRotation = Direction.North;

        private bool _isDirty;

        public bool IsDirty
        {
            get => _isDirty;
            set
            {
                if (_isDirty == value)
                    return;

                _isDirty = value;
                OnDirty?.Invoke();
            }
        }

        private ISawmill _sawmill;

        private MarkingsViewModel _markingsModel = new();

        public event Action? OnDirty;

        public RfHumanoidProfileEditor()
        {
            IoCManager.InjectDependencies(this);
            InitializeComponent();
            _sawmill = _logManager.GetSawmill("profile.editor");

            // the titles of the tabs are attached to the pages
            Tabs.Tabs[0].Title = Loc.GetString("humanoid-profile-editor-appearance-tab");
            Tabs.Tabs[1].Title = Loc.GetString("humanoid-profile-editor-skills-tab");
            Tabs.Tabs[2].Title = Loc.GetString("humanoid-profile-editor-traits-tab");

            var maxNameLength = _cfgManager.GetCVar(CCVars.MaxNameLength);
            _allowFlavorText = _cfgManager.GetCVar(CCVars.FlavorText);

            Markings.SetModel(_markingsModel);

            NameEdit.OnTextChanged += args => SetName(args.Text);
            NameRandomize.OnPressed += _ => RandomizeName();
            RandomizeEverythingButton.OnPressed += _ => RandomizeEverything();
            ImportButton.OnPressed += _ => ImportProfile();
            ExportButton.OnPressed += _ => ExportProfile();
            ExportImageButton.OnPressed += _ => ExportImage();
            OpenImagesButton.OnPressed += _ => OpenImages();
            Skin.OnValueChanged += _ => OnSkinColorOnValueChanged();
            SpriteRotateLeft.OnPressed += _ => RotatePreviewLeft();
            SpriteRotateRight.OnPressed += _ => RotatePreviewRight();
            SpeciesInfoButton.OnPressed += OnSpeciesInfoButtonPressed;
            ShowClothes.OnToggled += _ => ReloadPreview();

            #region Left

            #region Name

            NameEdit.IsValid = args => args.Length <= maxNameLength;
            WarningLabel.Text = Loc.GetString("humanoid-profile-editor-naming-rules-warning");

            #endregion Name

            #region Appearance

            #region Sex

            SexButton.OnItemSelected += args =>
            {
                SetSex((Sex) args.Value!);
            };

            #endregion Sex

            #region Age

            AgeEdit.OnTextChanged += args =>
            {
                if (!int.TryParse(args.Text, out var newAge))
                    return;

                SetAge(newAge);
            };

            #endregion Age

            #region Gender

            PronounsButton.AddItem(Loc.GetString("humanoid-profile-editor-pronouns-male-text"), Gender.Male);
            PronounsButton.AddItem(Loc.GetString("humanoid-profile-editor-pronouns-female-text"), Gender.Female);
            PronounsButton.AddItem(Loc.GetString("humanoid-profile-editor-pronouns-epicene-text"), Gender.Epicene);
            PronounsButton.AddItem(Loc.GetString("humanoid-profile-editor-pronouns-neuter-text"), Gender.Neuter);

            PronounsButton.OnItemSelected += args =>
            {
                SetGender((Gender) args.Value!);
            };

            #endregion Gender

            RefreshSpecies();

            SpeciesButton.OnItemSelected += args =>
            {
                SetSpecies((string) args.Value!);
                OnSkinColorOnValueChanged();
            };

            #region Skin

            RgbSkinColorContainer.AddChild(_rgbSkinColorSelector = new RfColorSelectorSliders());
            _rgbSkinColorSelector.SelectorType = RfColorSelectorSliders.ColorSelectorType.Hsv; // defaults color selector to HSV
            _rgbSkinColorSelector.OnColorChanged += _ =>
            {
                OnSkinColorOnValueChanged();
            };

            #endregion

            #region Eyes

            EyeColorPicker.OnColorChanged += newColor =>
            {
                if (Profile is null)
                    return;
                Profile = Profile.WithCharacterAppearance(
                    Profile.Appearance.WithEyeColor(newColor));
                _markingsModel.SetOrganEyeColor(Profile.Appearance.EyeColor);
                ReloadProfilePreview();
            };

            #endregion Eyes

            #endregion Appearance

            RefreshTraits();

            #region Markings

            _markingsModel.MarkingsChanged += (_, _) => OnMarkingChange();
            _markingsModel.MarkingsReset += OnMarkingChange;

            #endregion Markings

            RefreshFlavorText();

            #region Dummy

            #endregion Dummy

            #region Skills

            SkillsTab.OnSkillUpdate += args =>
            {
                Profile = Profile?.WithSkillPreference(args.Skill, args.Level, args.LeftPoints, _prototypeManager);
                SetDirty();
                SkillsTab.Refresh(Profile);
            };

            #endregion

            #endregion Left

            UpdateSpeciesGuidebookIcon();
            IsDirty = false;
        }

        private void OpenImages() => _resManager.UserData.OpenOsWindow(ContentSpriteSystem.Exports);

        private void RotatePreviewLeft()
        {
            _previewRotation = _previewRotation.TurnCw();
            SetPreviewRotation(_previewRotation);
        }

        private void RotatePreviewRight()
        {
            _previewRotation = _previewRotation.TurnCcw();
            SetPreviewRotation(_previewRotation);
        }

        private void SetDirty()
        {
            // If it equals default then reset the button.
            if (Profile == null || OriginalProfile?.MemberwiseEquals(Profile) == true)
            {
                IsDirty = false;
                return;
            }

            // TODO: Check if profile matches default.
            IsDirty = true;
        }

        /// <summary>
        /// Reloads the entire dummy entity for preview.
        /// </summary>
        /// <remarks>
        /// This is expensive so not recommended to run if you have a slider.
        /// </remarks>
        private void ReloadPreview()
        {
            if (Profile == null)
                return;

            SpriteView.LoadPreview(Profile, JobOverride, ShowClothes.Pressed);

            // Check and set the dirty flag to enable the save/reset buttons as appropriate.
            SetDirty();
        }

        /// <summary>
        /// Resets the profile to the defaults.
        /// </summary>
        public void ResetToDefault()
        {
            SetProfile(
                _preferencesManager.Preferences?.SelectedCharacter,
                _preferencesManager.Preferences?.SelectedCharacterIndex);
        }

        /// <summary>
        /// Sets the editor to the specified profile with the specified slot.
        /// </summary>
        public void SetProfile(HumanoidCharacterProfile? profile, int? slot)
        {
            Visible = profile != null && slot != null;
            OriginalProfile = profile?.Clone();
            Profile = profile?.Clone();
            CharacterSlot = slot;
            IsDirty = false;

            UpdateNameEdit();
            UpdateFlavorTextEdit();
            UpdateSexControls();
            UpdateGenderControls();
            UpdateSkinColor();
            UpdateAgeEdit();
            UpdateEyePickers();
            UpdateMarkings();

            RefreshLoadouts();
            RefreshSpecies();
            RefreshTraits();
            RefreshFlavorText();
            ReloadPreview();

            SkillsTab.Refresh(profile);
        }

        /// <summary>
        /// A slim reload that only updates the entity itself and not any of the job entities, etc.
        /// </summary>
        private void ReloadProfilePreview()
        {
            if (Profile == null)
                return;

            SpriteView.ReloadProfilePreview(Profile);

            // Check and set the dirty flag to enable the save/reset buttons as appropriate.
            SetDirty();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (!disposing)
                return;

            _loadoutWindow?.Dispose();
            _loadoutWindow = null;
        }

        protected override void EnteredTree()
        {
            base.EnteredTree();
            ReloadPreview();
        }

        private void SetPreviewRotation(Direction direction)
        {
            SpriteView.OverrideDirection = (Direction)((int)direction % 4 * 2);
        }
    }
}
