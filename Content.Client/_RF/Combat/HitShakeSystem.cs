using System.Numerics;
using Content.Shared._RF.Combat.HitShake;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;
using Robust.Shared.Random;

namespace Content.Client._RF.Combat;

/// <summary>
/// The shake of a sprite on a hit (<see cref="HitShakeEvent"/>):
/// it is pushed along the blow and then shakes in all directions with the
/// fading, the stronger the hit, the farther and the longer.
/// It is an animation of the sprite offset, like the vanilla lunge.
/// </summary>
public sealed partial class HitShakeSystem : EntitySystem
{
    [Dependency] private AnimationPlayerSystem _animation = null!;
    [Dependency] private IRobustRandom _random = null!;

    private const string AnimationKey = "rf-hit-shake";

    [SubscribeNetworkEvent]
    private void OnHitShake(HitShakeEvent msg)
    {
        var uid = GetEntity(msg.Target);
        if (!TryComp(uid, out HitShakeComponent? shake) || !HasComp<SpriteComponent>(uid))
            return;

        // A new hit replaces the shake that is still going, the stronger one is not cut by a weaker one
        if (_animation.HasRunningAnimation(uid, AnimationKey))
            _animation.Stop(uid, null, AnimationKey);

        _animation.Play(uid, GetAnimation(shake, msg.Strength, msg.Direction), AnimationKey);
    }

    private Animation GetAnimation(HitShakeComponent shake, float strength, Vector2 direction)
    {
        var length = MathHelper.Lerp(shake.MinDuration, shake.MaxDuration, strength);
        var amplitude = shake.MaxOffset * strength;
        var step = 1f / Math.Max(shake.Frequency, 1f);
        var swings = Math.Max(2, (int) (length / step));

        var frames = new List<AnimationTrackProperty.KeyFrame> { new(Vector2.Zero, 0f) };

        // It shakes in all directions: each swing goes to a random place around the sprite, always far from the previous one (the angle
        // turns by more than a quarter of a circle), so the sprite does not swing along a line like on a spring. The blow only
        // pushes the first swing, along its direction.
        var angle = direction == Vector2.Zero ? _random.NextAngle() : direction.ToAngle();

        for (var i = 0; i < swings; i++)
        {
            var fade = 1f - (float) i / swings;
            var radius = amplitude * fade * _random.NextFloat(0.6f, 1f);

            if (i == 0 && direction != Vector2.Zero)
                radius = amplitude * 1.2f;
            else
                angle += Angle.FromDegrees(_random.NextFloat(100f, 260f));

            frames.Add(new AnimationTrackProperty.KeyFrame(angle.ToVec() * radius, (i + 1) * step));
        }

        frames.Add(new AnimationTrackProperty.KeyFrame(Vector2.Zero, (swings + 1) * step));

        var animation = new Animation { Length = TimeSpan.FromSeconds((swings + 1) * step) };
        var track = new AnimationTrackComponentProperty
        {
            ComponentType = typeof(SpriteComponent),
            Property = nameof(SpriteComponent.Offset),
            InterpolationMode = AnimationInterpolationMode.Linear,
        };
        track.KeyFrames.AddRange(frames);
        animation.AnimationTracks.Add(track);
        return animation;
    }
}
