using Content.Shared._RF.Control;
using Content.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;

namespace Content.Server._RF.Control;

public sealed class MouseDragMoveSystem : SharedMouseDragMoveSystem
{
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<MouseDragVelocityRequest>(OnDragVelocityRequest);

        CommandBinds.Builder
            .Bind(ContentKeyFunctions.DragMove, new PointerStateInputCmdHandler(OnDragEnabled, OnDragDisabled))
            .Register<SharedMouseDragMoveSystem>();
    }

    private bool OnDragEnabled(ICommonSession? playerSession, EntityCoordinates coords, EntityUid entityUid)
    {
        return OnDragPressed(playerSession, true);
    }

    private bool OnDragDisabled(ICommonSession? playerSession, EntityCoordinates coords, EntityUid entityUid)
    {
        return OnDragPressed(playerSession, false);
    }

    private bool OnDragPressed(ICommonSession? playerSession, bool enabled)
    {
        if (playerSession?.AttachedEntity is not { Valid: true } player
            || !Exists(player)
            || !TryComp<MouseDragMoveComponent>(player, out _))
            return false;

        RaiseNetworkEvent(new MouseDragToggleMessage { Enabled = enabled });
        return true;
    }

    private void OnDragVelocityRequest(MouseDragVelocityRequest ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { Valid: true } player
            || !TryComp(player, out MouseDragMoveComponent? _)
            || !TryComp(player, out PhysicsComponent? body))
            return;

        _physics.SetLinearVelocity(player, ev.LinearVelocity, body: body);
        _physics.SetAngularVelocity(player, 0f, body: body);
    }
}
