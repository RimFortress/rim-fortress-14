using Content.Client._RF.UserInterface;
using System.Linq;
using Content.Client._RF.Suil;
using Content.Shared.Traits;
using Robust.Shared.Utility;

namespace Content.Client._RF.Lobby.UI;

public sealed partial class RfHumanoidProfileEditor
{
    /// <summary>
    /// Refreshes traits selector
    /// </summary>
    public void RefreshTraits()
    {
        TraitsList.RemoveAllChildren();

        var traits = _prototypeManager
            .EnumeratePrototypes<TraitPrototype>()
            .OrderBy(t => Loc.GetString(t.Name))
            .ToList();

        if (traits.Count < 1)
        {
            TraitsList.AddChild(CreateLabel(Loc.GetString("humanoid-profile-editor-no-traits"), SuilClass.ProfileHint));
            return;
        }

        // Setup model
        Dictionary<string, List<string>> traitGroups = new();
        List<string> defaultTraits = new();
        traitGroups.Add(TraitCategoryPrototype.Default, defaultTraits);

        foreach (var trait in traits)
        {
            if (trait.Category == null)
            {
                defaultTraits.Add(trait.ID);
                continue;
            }

            if (!_prototypeManager.HasIndex(trait.Category))
                continue;

            var group = traitGroups.GetOrNew(trait.Category);
            group.Add(trait.ID);
        }

        // Create UI view from model
        foreach (var (categoryId, categoryTraits) in traitGroups)
        {
            TraitCategoryPrototype? category = null;

            if (categoryId != TraitCategoryPrototype.Default)
            {
                category = _prototypeManager.Index<TraitCategoryPrototype>(categoryId);
                // Label
                var label = CreateLabel(Loc.GetString(category.Name), SuilClass.LabelHeading);
                label.Margin = new Thickness(0, 10, 0, 0);
                TraitsList.AddChild(label);
            }

            List<RfTraitPreferenceSelector?> selectors = new();
            var selectionCount = 0;

            foreach (var traitProto in categoryTraits)
            {
                var trait = _prototypeManager.Index<TraitPrototype>(traitProto);
                var selector = new RfTraitPreferenceSelector(trait);

                selector.Preference = Profile?.TraitPreferences.Contains(trait.ID) == true;
                if (selector.Preference)
                    selectionCount += trait.Cost;

                selector.PreferenceChanged += preference =>
                {
                    if (preference)
                    {
                        Profile = Profile?.WithTraitPreference(trait.ID, _prototypeManager);
                    }
                    else
                    {
                        Profile = Profile?.WithoutTraitPreference(trait.ID, _prototypeManager);
                    }

                    SetDirty();
                    RefreshTraits(); // If too many traits are selected, they will be reset to the real value.
                };
                selectors.Add(selector);
            }

            // Selection counter
            if (category is { MaxTraitPoints: >= 0 })
            {
                TraitsList.AddChild(CreateLabel(
                    Loc.GetString("humanoid-profile-editor-trait-count-hint", ("current", selectionCount), ("max", category.MaxTraitPoints)),
                    SuilClass.ProfileHint));
            }

            foreach (var selector in selectors)
            {
                if (selector == null)
                    continue;

                if (category is { MaxTraitPoints: >= 0 } &&
                    selector.Cost + selectionCount > category.MaxTraitPoints)
                {
                    selector.OverLimit = true;
                }

                TraitsList.AddChild(selector);
            }
        }
    }

    private static UiElement CreateLabel(string text, string styleClass)
    {
        var label = new UiElement { Text = text };
        label.AddSuilStyleClass(styleClass);
        return label;
    }
}
