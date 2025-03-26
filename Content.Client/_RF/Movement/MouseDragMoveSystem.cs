using System.Numerics;
using Content.Shared._RF.Movement;
using Content.Shared.Input;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Shared.Input;
using Robust.Shared.Timing;

namespace Content.Client._RF.Movement;

/// <summary>
/// This handles entities with <see cref="MouseDragMoveComponent"/>
/// </summary>
public sealed class MouseDragMoveSystem : SharedMouseDragMoveSystem
{
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly InputSystem _inputSystem = default!;
    [Dependency] private readonly IEyeManager _eyeManager = default!;
    [Dependency] private readonly IInputManager _inputManager = default!;
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;

    private bool Enabled { get; set; }

    private EntityUid? _dragging;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<MouseDragToggleMessage>(OnToggle);
    }

    private void OnToggle(MouseDragToggleMessage msg, EntitySessionEventArgs args)
    {
        Enabled = msg.Enabled;

        if (Enabled && args.SenderSession.AttachedEntity is { Valid: true } player)
            StartDragging(player);
        else
            StopDragging();
    }

    private void StartDragging(EntityUid entity)
    {
        if (!TryComp(entity, out MouseDragMoveComponent? _))
            return;

        _dragging = entity;
    }

    private void StopDragging()
    {
        Enabled = false;
        _dragging = null;

        RaiseNetworkEvent(new MouseDragVelocityRequest { LinearVelocity = Vector2.Zero });
    }

    // This code is mostly taken from GridDraggingSystem.cs.
    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (!Enabled || !_gameTiming.IsFirstTimePredicted || _dragging is not { } entity)
            return;

        var state = _inputSystem.CmdStates.GetState(ContentKeyFunctions.DragMove);

        if (state != BoundKeyState.Down)
        {
            StopDragging();
            return;
        }

        var mouseScreenPos = _inputManager.MouseScreenPosition;
        var mouseMapPos = _eyeManager.PixelToMap(mouseScreenPos);

        if (!TryComp(entity, out MouseDragMoveComponent? dragMove)
            || !TryComp(entity, out TransformComponent? xform)
            || xform.MapID != mouseMapPos.MapId)
        {
            StopDragging();
            return;
        }

        // Sets the direction of movement of the entity along the mouse direction
        var tickTime = _gameTiming.TickPeriod;
        var distance = mouseMapPos.Position - _transformSystem.GetWorldPosition(xform);
        var velocity = distance.LengthSquared() > 0f ? (distance / (float)tickTime.TotalSeconds) * 0.25f : Vector2.Zero;

        if (velocity.LengthSquared() > dragMove.MaxSpeed * dragMove.MaxSpeed)
            velocity = Vector2.Normalize(velocity) * dragMove.MaxSpeed;

        RaiseNetworkEvent(new MouseDragVelocityRequest { LinearVelocity = velocity });
    }
}
