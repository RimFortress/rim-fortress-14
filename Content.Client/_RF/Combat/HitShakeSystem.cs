using System.Numerics;
using Content.Shared._RF.Combat.HitShake;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;
using Robust.Shared.Random;

namespace Content.Client._RF.Combat;

/// <summary>
/// The shake of a sprite on a hit (<see cref="HitShakeEvent"/>):
/// it is thrown along the blow and swings back and forth with the
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

        // Along the blow, or in a random direction if the source is unknown, with a slight scatter so that the hits are not alike
        var axis = direction == Vector2.Zero ? _random.NextAngle().ToVec() : direction;
        var side = new Vector2(-axis.Y, axis.X);

        var frames = new List<AnimationTrackProperty.KeyFrame> { new(Vector2.Zero, 0f) };
        for (var i = 0; i < swings; i++)
        {
            var fade = 1f - (float) i / swings;
            var sign = i % 2 == 0 ? 1f : -1f;
            // The first swing is the blow itself: farther, then it swings back with less each time
            var along = axis * sign * amplitude * fade * (i == 0 ? 1.4f : 1f);
            var across = side * _random.NextFloat(-0.35f, 0.35f) * amplitude * fade;
            frames.Add(new AnimationTrackProperty.KeyFrame(along + across, (i + 1) * step));
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
