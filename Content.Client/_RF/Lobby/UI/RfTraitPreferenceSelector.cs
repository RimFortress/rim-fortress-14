using Content.Suil;
using Content.Client._RF.Suil;
using Content.Client._RF.UserInterface;
using Content.Client._RF.UserInterface.Controls;
using Content.Shared.Traits;

namespace Content.Client._RF.Lobby.UI;

/// <summary>
/// The check box of a trait in the traits tab of the profile editor, the <see cref="RfCheckBox"/> version of
/// <see cref="Content.Client.Lobby.UI.Roles.TraitPreferenceSelector"/>.
/// </summary>
/// <remarks>
/// While <see cref="OverLimit"/> is set the check box has the class <c>over-limit</c>.
/// </remarks>
[SuilTag("trait-preference-selector")]
public sealed class RfTraitPreferenceSelector : RfCheckBox
{
    public int Cost { get; }

    public bool Preference
    {
        get => Pressed;
        set => Pressed = value;
    }

    /// <summary>
    /// The trait doesn't fit in the points of its category.
    /// </summary>
    public bool OverLimit
    {
        get;
        set
        {
            field = value;
            if (value)
                AddSuilStyleClass(SuilClass.OverLimit);
            else
                RemoveSuilStyleClass(SuilClass.OverLimit);
        }
    }

    public event Action<bool>? PreferenceChanged;

    public RfTraitPreferenceSelector(TraitPrototype trait)
    {
        AlignSelf = SuilAlignSelf.FlexStart;

        var text = trait.Cost != 0 ? $"[{trait.Cost}] " : "";
        text += Loc.GetString(trait.Name);

        Cost = trait.Cost;
        Text = text;
        OnToggled += _ => PreferenceChanged?.Invoke(Preference);

        if (trait.Description is { } desc)
            ToolTip = Loc.GetString(desc);
    }
}
