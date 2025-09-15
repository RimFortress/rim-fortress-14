using System.Numerics;
using Content.Client._RF.Info.Controls;
using Content.Client._RF.UserInterface.Controllers;
using Content.Shared._RF.Info;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._RF.Info;

public sealed class PopInfoUIController : WindowUiController<PopInfoWindow>
{
    [Dependency] private readonly IEntityManager _entity = default!;
    [Dependency] private readonly IEntityNetworkManager _net = default!;
    [Dependency] private readonly IEyeManager _eye = default!;

    public event Action<EntityHealthInfoResponse>? OnHealthInfo;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<EntityHealthInfoResponse>(OnHealthInfoResponse);
    }

    private void OnHealthInfoResponse(EntityHealthInfoResponse msg, EntitySessionEventArgs args)
    {
        OnHealthInfo?.Invoke(msg);
    }

    public void OpenWindow(EntityUid uid)
    {
        OpenWindow();
        Window!.SetInfo(uid);

        var transform = _entity.System<TransformSystem>();

        var coords = _entity.GetComponent<TransformComponent>(uid).Coordinates;
        var screenCoords = _eye.MapToScreen(transform.ToMapCoordinates(coords));
        var uiCoords = UIManager.ScreenToUIPosition(screenCoords);

        SetWindowPos(uiCoords.Position, 16f);
    }

    public void OpenWindow(EntityUid uid, Vector2 targetPos)
    {
        OpenWindow();
        Window!.SetInfo(uid);
        SetWindowPos(targetPos, 16f);
    }

    private void SetWindowPos(Vector2 targetPos, float offset = 0)
    {
        if (Window is not { Disposed: false })
            return;

        var vSize = UIManager.ActiveScreen?.Size ?? Vector2.Zero;
        var wSize = Window!.SetSize;

        var haveRightSpace = targetPos.X + wSize.X + offset <= vSize.X;
        var haveBottomSpace = targetPos.Y + wSize.Y + offset <= vSize.Y;

        var x = haveRightSpace ? targetPos.X + offset : targetPos.X - wSize.X - offset;
        var y = haveBottomSpace ? targetPos.Y + offset : targetPos.Y - wSize.Y - offset;

        var windowPos = new Vector2(
            Math.Clamp(x, 0, vSize.X),
            Math.Clamp(y, 0, vSize.Y));

        LayoutContainer.SetPosition(Window, windowPos);
    }

    public void HealthInfoRequest(EntityUid uid)
    {
        var netUid = _entity.GetNetEntity(uid);
        _net.SendSystemNetworkMessage(new EntityHealthInfoRequest(netUid));
    }
}

