using Content.Shared._RF.Social.Components;
using Content.Shared.Movement.Systems;

namespace Content.Shared._RF.Social.Systems;

/// <summary>
/// Manages <see cref="ModifySpeedOnMoodComponent"/>
/// </summary>
public sealed class ModifySpeedOnMoodSystem : EntitySystem
{
    [Dependency] private readonly SocialSystem _social = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<ModifySpeedOnMoodComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovementSpeedModifiers);
    }

    private void OnRefreshMovementSpeedModifiers(
        EntityUid uid,
        ModifySpeedOnMoodComponent component,
        RefreshMovementSpeedModifiersEvent ev)
    {
        ev.ModifySpeed(1 + _social.GetMood(uid) * component.MoodFactor);
    }
}
