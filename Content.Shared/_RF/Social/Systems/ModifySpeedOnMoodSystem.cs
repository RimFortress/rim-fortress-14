using Content.Shared._RF.Social.Components;
using Content.Shared.Movement.Systems;

namespace Content.Shared._RF.Social.Systems;

/// <summary>
/// Manages <see cref="ModifySpeedOnMoodComponent"/>
/// </summary>
public sealed partial class ModifySpeedOnMoodSystem : EntitySystem
{
    [Dependency] private SocialSystem _social = default!;

    [SubscribeLocalEvent]
    private void OnRefreshMovementSpeedModifiers(
        Entity<ModifySpeedOnMoodComponent> ent,
        ref RefreshMovementSpeedModifiersEvent ev)
    {
        ev.ModifySpeed(1 + _social.GetMood(ent.Owner) * ent.Comp.MoodFactor);
    }
}
